using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DdamaBoard : MonoBehaviour {
    public const int boardSize = 8;

    public Piece[,] board = new Piece[boardSize, boardSize];
    public GameObject yellowPiecePrefab;
    public GameObject blackPiecePrefab;

	// Use this for initialization
	void Start () {
        GenerateBoard();
	}

    private void GenerateBoard() {
        for (int row = 0; row < 2; row++)
        {
            for (int x = 0; x < boardSize; x++)
            {
                GeneratePiece(Piece.Team.Yellow, x, row + 1);
                GeneratePiece(Piece.Team.Black, x, boardSize - 2 - row);
            }
        }
    }

    private void GeneratePiece(Piece.Team team, int x, int y) {
        GameObject piecePrefab = (team == Piece.Team.Yellow)
            ? yellowPiecePrefab : blackPiecePrefab;

        GameObject go = Instantiate(piecePrefab) as GameObject;
        go.transform.SetParent(transform);
        Piece p = go.GetComponent<Piece>();
        board[x, y] = p;
        MovePiece(p, x, y);
    }

    private void MovePiece(Piece p, int x, int y) {
        p.transform.position =
             (Vector3.right * x) +
             (Vector3.forward * y) +
             new Vector3(-4.0f, 0, -4.0f) +
             new Vector3(0.5f, 0, 0.5f);
    }
}
