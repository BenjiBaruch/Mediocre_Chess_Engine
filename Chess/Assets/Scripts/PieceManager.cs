using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceManager : MonoBehaviour
{
    // Help from https://docs.unity3d.com/Manual/InstantiatingPrefabs.html
    PieceObject[] pieces;
    public Sprite[] pieceSprites;
    public GameObject Prefab;

    // Start is called before the first frame update
    void Start()
    {
        int[] board = Board.StartingBoard();
        pieces = new PieceObject[32];
        int j = 0;
        for (int i = 0; i < 64; i++) {
            if (board[i] == Piece.Empty || board[i] == -1) {
                continue;
            }
            Sprite sprite = pieceSprites[board[i] switch {
                Piece.White | Piece.King => 0,
                Piece.Black | Piece.King => 1,
                Piece.White | Piece.Queen => 2,
                Piece.Black | Piece.Queen => 3,
                Piece.White | Piece.Rook => 4,
                Piece.Black | Piece.Rook => 5,
                Piece.White | Piece.Knight => 6,
                Piece.Black | Piece.Knight => 7,
                Piece.White | Piece.Bishop => 8,
                Piece.Black | Piece.Bishop => 9,
                Piece.White | Piece.Pawn => 10,
                Piece.Black | Piece.Pawn => 11,
                _ => 12
            }];
            GameObject instance = Instantiate(Prefab, PieceObject.CoordsFromPosition(i), Quaternion.identity);
            // https://docs.unity3d.com/ScriptReference/ScriptableObject.CreateInstance.html
            PieceObject piece = (PieceObject) ScriptableObject.CreateInstance(typeof(PieceObject));
            piece.SetParams(i, board[i], sprite, instance);
            pieces[j++] = piece;
        }
    }
}
