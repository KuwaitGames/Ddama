using UnityEngine;


public class DdamaBoard : MonoBehaviour {
    public GameObject yellowPiecePrefab;
    public GameObject blackPiecePrefab;

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

        dragSourceBlock = hoverBlock;
    }

    // If we've been dragging a piece, try to make the
    // move.
    private void Drop() {
        if (dragSourceBlock.IsNone)
            return;

        if (IsValidKillMove(dragSourceBlock, hoverBlock)) {
            PerformKillMove(dragSourceBlock, hoverBlock);
            CheckSheikhPromotion(hoverBlock);
            SwitchTurns();
        } else if (IsValidMove(dragSourceBlock, hoverBlock)) {
            PerformMove(dragSourceBlock, hoverBlock);
            CheckSheikhPromotion(hoverBlock);
            SwitchTurns();
        } else {
            MovePiece(dragSourceBlock);
        }

        dragSourceBlock = Block.none;
    }

    private void SwitchTurns() {
        turn = (turn == Piece.Team.Yellow) ? Piece.Team.Black : Piece.Team.Yellow;
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

        // Can't move backwards if not a sheik
        int direction = (to.Y - from.Y) * (p.team == Piece.Team.Yellow ? 1 : -1);
        if (!p.isSheikh && direction < 0)
            return false;

        // Can't move by more than one block if not a sheik
        int distance = Mathf.Abs(to.Y - from.Y) + Mathf.Abs(to.X - from.X);
        if (!p.isSheikh && distance != 1)
            return false;

        return true;
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

        // Can't kill backwards if not a sheikh
        int direction = (to.Y - from.Y) * (attacker.team == Piece.Team.Yellow ? 1 : -1);
        if (!attacker.isSheikh && direction < 0)
            return false;

        // Can't kill a distant victim if not a sheik
        int distance = Mathf.Abs(to.Y - from.Y) + Mathf.Abs(to.X - from.X);
        if (!attacker.isSheikh && distance != 2)
            return false;
            
        Piece victim = PieceForBlock(KillVictimBlock(from, to));

        if (victim == null)
            return false;

        return attacker.team != victim.team;
    }

    private void MovePiece(Block block) {
        PieceForBlock(block).transform.position =
             (Vector3.right * block.X) +
             (Vector3.forward * block.Y) +
             boardOffset +
             pieceOffset;
    }
}
