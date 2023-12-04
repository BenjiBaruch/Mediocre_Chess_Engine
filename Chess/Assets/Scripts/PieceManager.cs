using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceManager : MonoBehaviour
{
    // Help from https://docs.unity3d.com/Manual/InstantiatingPrefabs.html
    // pieces variable stores all 32 pieces generated at start of the chess game
    PieceObject[] pieces; 
    // pieceSprites contains all 12 images of chess pieces, set in Unity editor
    public Sprite[] pieceSprites;
    // Prefab contains the base GameObject of each piece, set in Unity editor
    public GameObject Prefab;
    // Stores whether mouse is currently down
    bool mouseDrag = false;
    #nullable enable
    // Stores piece that is currently being held down by mouse
    PieceObject? selectedPiece;
    #nullable disable
    // Stores difference between mouse position and piece position
    Vector3 mouseOffset = new(0, 0, 0);
    // Stores difference between camera and board
    static Vector3 cameraOffest = new(0, 0, 10);
    // Determines how quickly the piece will move towards the mouse (or how quickly mouseOffset approaches zero)
    public static Vector3 MouseGravity = new(0.8F, 0.8F, 0.8F);

    // Start is called before the first frame update
    void Start()
    {
        // Grabs starting position from Board class
        int[] board = Board.StartingBoard();
        pieces = new PieceObject[32];
        int j = 0;
        for (int i = 0; i < 64; i++) {
            // Skips piece generation for empty or invalid tiles
            if (board[i] == Piece.Empty || board[i] == -1) {
                continue;
            }
            // Sets piece sprite
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
            // Creates GameObject for piece
            GameObject instance = Instantiate(
                Prefab, // Inherits from Prefab object 
                PieceObject.CoordsFromPosition(i), // Positions it at correct position
                Quaternion.identity // Starts with no rotation
                );
            // https://docs.unity3d.com/ScriptReference/ScriptableObject.CreateInstance.html
            // Creates PieceObject class to wrap instance GameObject
            PieceObject piece = (PieceObject) ScriptableObject.CreateInstance(typeof(PieceObject));
            piece.SetParams(i, board[i], sprite, instance);
            // Stores PieceObject in array
            pieces[j++] = piece;
        }
    }

    void Update() {
        // Help from https://docs.unity3d.com/ScriptReference/Input.GetMouseButton.html
        // Help from https://docs.unity3d.com/ScriptReference/Input-mousePosition.html
        // Help from https://www.youtube.com/watch?v=5NTmxDSKj-Q
        // Mouse position determined, relative to board instead of camera
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition) + cameraOffest;
        if (Input.GetMouseButton(0)) {
            if (mouseDrag) {
                // If mouse is currently down and dragging
                if (selectedPiece != null) { // If a piece is selected
                    // Decrease mouse offset (move piece towards mouse)
                    mouseOffset = Vector3.Scale(mouseOffset, MouseGravity);
                    // Update piece position
                    selectedPiece.SetPosition(mousePosition - mouseOffset);
                }
            } else {
                // If mouse is down but not yet dragging
                mouseDrag = true;
                // Determine which tile the mouse is hovering over
                int mouseTile = PieceObject.PositionFromCoords(mousePosition);
                if (mouseTile > -1) {
                    selectedPiece = null;
                    foreach (PieceObject p in pieces) {
                        if (p.Position == mouseTile) {
                            // If mouse is hovering over a piece
                            // Select piece
                            selectedPiece = p;
                            // Determine difference between mouse and piece position
                            mouseOffset = mousePosition - p.Coords;
                            break;
                        }
                    }
                }
                // Debug.Log(selectedPiece + ": " + mouseOffset);
            }
        } else if (mouseDrag) {
            // If mouse released
            mouseDrag = false;
            if (selectedPiece != null) {
                // Place piece where mosue currently is
                selectedPiece.PlacePiece(mousePosition - mouseOffset, pieces);
                // Deselect piece
                selectedPiece = null;
            }
        }
        // Update all pieces
        foreach (PieceObject p in pieces) p.Update();
    }
}
