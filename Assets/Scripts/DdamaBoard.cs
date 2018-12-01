using System.Collections.Generic;
using UnityEngine;


public class DdamaBoard : MonoBehaviour {
    public GameObject yellowPiecePrefab;
    public GameObject blackPiecePrefab;

    public Lander lander;

    private struct Block {
        private readonly int x, y;

        public Block(int x, int y) {
            this.x = x;
            this.y = y;
        }

        public static readonly Block none = new Block(-1, -1);

        public override string ToString() { return "(" + x + ", " + y + ")"; }

        public int X {get {return x;}}
        public int Y {get {return y;}}
        public bool IsNone {get {return this == Block.none;}}

        public override bool Equals(System.Object obj) {
            if ((obj == null) || !this.GetType().Equals(obj.GetType())) {
                return false;
            }

            Block other = (Block)obj;
            return x == other.x && y == other.y;
        }

        public override int GetHashCode() {
            return (x << 2) ^ y;
        }

        public static bool operator==(Block lhs, Block rhs) {
            return lhs.Equals(rhs);
        }

        public static bool operator!=(Block lhs, Block rhs) {
            return !(lhs.Equals(rhs));
        }
    }

    private const int boardSize = 8;

    private Piece[,] board = new Piece[boardSize, boardSize];
    private Piece.Team turn = Piece.Team.Yellow;

    private readonly Vector3 boardOffset = new Vector3(-4.0f, 0, -4.0f);
    private readonly Vector3 pieceOffset = new Vector3(0.5f, 0, 0.5f);

    private Block hoverBlock = Block.none;
    private Block dragSourceBlock = Block.none;

    private List<Block[]> killMovesList = new List<Block[]>();

    private Block miniGamePlayer = Block.none;

    // Use this for initialization
    void Start () {
        GenerateBoard();
	}

    private void GenerateBoard()
    {
        for (int row = 0; row < 2; row++)
        {
            for (int x = 0; x < boardSize; x++)
            {

                GeneratePiece(Piece.Team.Yellow, new Block(x, row + 1));
                GeneratePiece(Piece.Team.Black, new Block(x, boardSize - 2 - row));
            }
        }
    }

    private void GeneratePiece(Piece.Team team, Block block)
    {
        GameObject piecePrefab = (team == Piece.Team.Yellow)
            ? yellowPiecePrefab : blackPiecePrefab;

        GameObject go = Instantiate(piecePrefab) as GameObject;
        go.transform.SetParent(transform);
        Piece p = go.GetComponent<Piece>();
        board[block.X, block.Y] = p;
        MovePiece(block);
    }

    private void Update() {
        UpdateHoverBlock();
        UpdatePieceDrag();

        if (Input.GetMouseButtonDown(0))
            Drag();

        if (Input.GetMouseButtonUp(0))
            Drop();
    }

    // Cast a ray from the camera to the mouse pointer
    // and find it where does it hits the board.
    private void UpdateHoverBlock() {
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        Block newBlock = Block.none;
        if (Physics.Raycast(ray, out hit, 25.0f, LayerMask.GetMask("Board"))) {
            newBlock = new Block(
                (int)(hit.point.x - boardOffset.x),
                (int)(hit.point.z - boardOffset.z)
            );

            if (!IsValidBlock(newBlock))
                newBlock = Block.none;
        }

        hoverBlock = newBlock;
    }

    // See if there's a piece that is eligible for a drag.
    private void Drag() {
        if (hoverBlock.IsNone)
            return;

        if (PieceForBlock(hoverBlock) == null)
            return;

        if (!IsMovable(hoverBlock))
            return;

        dragSourceBlock = hoverBlock;
    }

    // If we've been dragging a piece, try to make the
    // move.
    private void Drop() {
        if (dragSourceBlock.IsNone)
            return;

        if (IsValidKillMove(dragSourceBlock, hoverBlock)) {
            PerformKillMove(dragSourceBlock, hoverBlock);
            CompleteTurn();
            CheckGameOver();
        } else if (killMovesList.Count == 0 && IsValidMove(dragSourceBlock, hoverBlock)) {
            PerformMove(dragSourceBlock, hoverBlock);
            CompleteTurn();
        } else {
            MovePiece(dragSourceBlock);
        }

        dragSourceBlock = Block.none;
    }

    private void CompleteTurn() {
        CheckSheikhPromotion(hoverBlock);

        turn = (turn == Piece.Team.Yellow) ? Piece.Team.Black : Piece.Team.Yellow;
        UpdateKillMovesList();
    }

    private void CheckGameOver() {
        // if current player has no pieces left, then game is over
        for (int x = 0; x < boardSize; x++) {
            for (int y = 0; y < boardSize; y++) {
                Piece p = PieceForBlock(new Block(x, y));
                if (p != null && p.team == turn)
                    return; // found a piece, game is not over
            }
        }

        // player who has the turn lost :(
        CelebrateWinner();
        ResetGame();
    }

    private void CelebrateWinner() {
        // TODO
    }

    private void ResetGame() {
        // remove all existing pieces
        for (int x = 0; x < boardSize; x++) {
            for (int y = 0; y < boardSize; y++) {
                Piece p = PieceForBlock(new Block(x, y));
                if (p != null) {
                    RemovePiece(p);
                    board[x, y] = null;
                }
            }
        }
        
        // generate the board again
        GenerateBoard();

        // yellow always goes first
        turn = Piece.Team.Yellow;
    }

    private void CheckSheikhPromotion(Block block) {
        // if not at the last row return
        if (block.Y != ((turn == Piece.Team.Yellow) ? boardSize - 1 : 0))
            return;

        Piece p = PieceForBlock(block);

        // it is already a sheikh
        if (p.isSheikh)
            return;

        p.isSheikh = true;
        p.transform.SetParent(null);
        p.transform.Rotate(Vector3.right * 90);
    }

    private void PerformMove(Block from, Block to) {
        // insert ourselves in the new spot on the board
        board[to.X, to.Y] = PieceForBlock(from);

        // remove ourselves from the old spot on the board
        board[from.X, from.Y] = null;

        // move the piece to the correct block on the board
        MovePiece(to);
    }

    private void PerformKillMove(Block from, Block to) {
        // move the attacker
        PerformMove(from, to);

        // remove the victim
        Block victimBlock = KillVictimBlock(from, to);
        RemovePiece(PieceForBlock(victimBlock));
        board[victimBlock.X, victimBlock.Y] = null;

        // attacker plays minigame
        StartMiniGame(to);
    }

    private void RemovePiece(Piece p) {
        p.gameObject.SetActive(false);
    }

    private void UpdatePieceDrag() {
        if (dragSourceBlock.IsNone)
            return;

        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit, 25.0f, LayerMask.GetMask("Board"))) {
            PieceForBlock(dragSourceBlock).transform.position = hit.point + Vector3.up;
        }
    }

    private Piece PieceForBlock(Block b) {
        return board[b.X, b.Y];
    }

    private bool IsValidBlock(Block b) {
        return b.X >= 0 && b.X < boardSize && b.Y >= 0 && b.Y < boardSize;
    }

    private bool IsValidMove(Block from, Block to) {
        Piece p = PieceForBlock(from);

        // Block must be inhabitable
        if (!IsValidLandingBlock(to))
            return false;

        // Not your turn
        if (p.team != turn)
            return false;

        if (!IsValidPath(from, to, p.isSheikh))
            return false;

        return true;
    }

    private bool IsValidPath(Block from, Block to, bool isSheikh) {
        return isSheikh
                ? IsStraightClearPathBetween(from, to)
                : (!IsBackwardMove(from, to) && AreAdjacentBlocks(from, to));
    }

    private bool IsValidLandingBlock(Block target) {
        return IsValidBlock(target) && (PieceForBlock(target) == null);
    }

    private Block KillVictimBlock(Block from, Block to) {
        int x, y;

        if (from.X == to.X) {
            // front/back kill
            x = from.X;
            y = to.Y + (from.Y < to.Y ? -1 : 1);
        } else if (from.Y == to.Y) {
            // sideways kill
            y = from.Y;
            x = to.X + (from.X < to.X ? -1 : 1);
        } else {
            return Block.none;
        }

        return new Block(x, y);
    }

    private bool IsValidKillMove(Block from, Block to) {
        if (!IsValidLandingBlock(to))
            return false;

        Piece attacker = PieceForBlock(from);
        if (attacker == null)
            return false;

        // Not your turn
        if (attacker.team != turn)
            return false;

        Block victimBlock = KillVictimBlock(from, to);
        if (!IsValidBlock(victimBlock))
            return false;

        if (!IsValidPath(from, victimBlock, attacker.isSheikh))
            return false;

        Piece victim = PieceForBlock(victimBlock);
        if (victim == null)
            return false;

        return attacker.team != victim.team;
    }

    private bool IsBackwardMove(Block from, Block to) {
        return (to.Y - from.Y) * (turn == Piece.Team.Yellow ? 1 : -1) < 0;
    }

    private bool AreAdjacentBlocks(Block a, Block b) {
        return (Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y)) == 1;
    }

    private bool IsStraightClearPathBetween(Block from, Block to) {
        if (from.X == to.X) {
            int min = Mathf.Min(from.Y, to.Y) + 1;
            int max = Mathf.Max(from.Y, to.Y) - 1;

            for (int y = min; y <= max; y++)
                if (board[from.X, y] != null)
                    return false;
        } else if (from.Y == to.Y) {
            int min = Mathf.Min(from.X, to.X) + 1;
            int max = Mathf.Max(from.X, to.X) - 1;

            for (int x = min; x <= max; x++)
                if (board[x, from.Y] != null)
                    return false;
        } else {
            // Diagonal path always fails
            return false;
        }

        return true;
    }

    private bool IsMovable(Block from) {
        Piece p = PieceForBlock(from);

        // must not be blank
        if (p == null)
            return false;

        // must be our turn
        if (p.team != turn)
            return false;

        // if there are no current kills, we're good to go
        if (killMovesList.Count == 0)
            return true;

        // since there are possible kills, this has to be one of them
        if (killMovesList.Count > 0)
            foreach (Block[] killMove in killMovesList)
                if (killMove[0] == from)
                    return true;

        // this isn't one of the kill moves, so deny it
        return false;
    }

    private void UpdateKillMovesList() {
        killMovesList.Clear();
        for (int x = 0; x < boardSize; x++) {
            for (int y = 0; y < boardSize; y++) {
                Block block = new Block(x, y);
                Piece p = PieceForBlock(block);

                // skip empty blocks and other players blocks
                if (p == null || p.team != turn)
                    continue;

                foreach (Block target in FindKillMovesFromBlock(block))
                    killMovesList.Add(new [] { block, target });
            }
        }
    }

    private List<Block> FindKillMovesFromBlock(Block from) {
        List<Block> killMoves = new List<Block>();

        Piece p = PieceForBlock(from);
        if (p == null)
            return killMoves;

        if (p.team != turn)
            return killMoves;

        // right
        for (int x = from.X + 1; x < boardSize; x++) {
            if (board[x, from.Y] != null) {
                Block target = new Block(x + 1, from.Y);
                if (IsValidKillMove(from, target))
                    killMoves.Add(target);
                else
                    break;
            }
        }

        // left
        for (int x = from.X - 1; x >= 0; x--) {
            if (board[x, from.Y] != null) {
                Block target = new Block(x - 1, from.Y);
                if (IsValidKillMove(from, target))
                    killMoves.Add(target );
                else
                    break;
            }
        }

        // up
        for (int y = from.Y + 1; y < boardSize; y++) {
            if (board[from.X, y] != null) {
                Block target = new Block(from.X, y + 1);
                if (IsValidKillMove(from, target))
                    killMoves.Add(target);
                else
                    break;
            }
        }

        // down
        for (int y = from.Y - 1; y >= 0; y--) {
            if (board[from.X, y] != null)
            {
                Block target = new Block(from.X, y - 1);
                if (IsValidKillMove(from, target))
                    killMoves.Add(target);
                else
                    break;
            }
        }

        return killMoves;
    }

    private void MovePiece(Block block) {
        PieceForBlock(block).transform.position =
             (Vector3.right * block.X) +
             (Vector3.forward * block.Y) +
             boardOffset +
             pieceOffset;
    }

    private void StartMiniGame(Block player) {
        miniGamePlayer = player;
        Camera.main.transform.position = new Vector3(10.0f, 6.0f, 4.6f);
        Camera.main.transform.rotation = new Quaternion(0.2f, 0.0f, 0.0f, 1.0f);
        lander.PlayRound(PieceForBlock(player).team);
    }

    public void EndMiniGame(bool survived) {
        Camera.main.transform.position = new Vector3(0.0f, 8.0f, 0.0f);
        Camera.main.transform.rotation = new Quaternion(0.7f, 0.0f, 0.0f, 0.7f);

        if (!survived) {
            RemovePiece(PieceForBlock(miniGamePlayer));
            board[miniGamePlayer.X, miniGamePlayer.Y] = null;
        }
        
        miniGamePlayer = Block.none;
    }
}
