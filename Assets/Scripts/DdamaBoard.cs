using System.Collections;
using System.Collections.Generic;
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

        if (!IsValidMove(dragSourceBlock, hoverBlock)) {
            CancelDrag();
            return;
        }

        // insert ourselves in the new spot on the board
        board[hoverBlock.X, hoverBlock.Y] = PieceForBlock(dragSourceBlock);

        // remove ourselves from the old spot on the board
        board[dragSourceBlock.X, dragSourceBlock.Y] = null;

        // move the piece to the correct block on the board
        MovePiece(hoverBlock);

        // switch turns
        turn = (turn == Piece.Team.Yellow) ? Piece.Team.Black : Piece.Team.Yellow;

        // drag operation has ended
        dragSourceBlock = Block.none;
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

    private void CancelDrag() {
        MovePiece(dragSourceBlock);
        dragSourceBlock = Block.none;
    }

    private Piece PieceForBlock(Block b) {
        return board[b.X, b.Y];
    }

    private bool IsValidBlock(Block b) {
        return b.X >= 0 && b.X < boardSize && b.Y >= 0 && b.Y < boardSize;
    }

    private bool IsValidMove(Block from, Block to) {
        Debug.Log(from);
        Debug.Log(to);

        Piece p = PieceForBlock(from);

        // Can't move outside the board
        if (!IsValidBlock(to))
            return false;

        // Can't move to an occupied spot
        if (PieceForBlock(to) != null)
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

    private void MovePiece(Block block) {
        PieceForBlock(block).transform.position =
             (Vector3.right * block.X) +
             (Vector3.forward * block.Y) +
             boardOffset +
             pieceOffset;
    }
}
