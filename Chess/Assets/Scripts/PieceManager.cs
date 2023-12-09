using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEngine;

public class PieceManager : MonoBehaviour
{
    // Help from https://docs.unity3d.com/Manual/InstantiatingPrefabs.html
    // pieces variable stores all 32 pieces generated at start of the chess game
    List<PieceObject> pieces;
    List<PieceObject> whiteGraveyard;
    List<PieceObject> blackGraveyard;
    // pieceSprites contains all 12 images of chess pieces, set in Unity editor
    public Sprite[] pieceSprites;
    // Prefab contains the base GameObject of each piece, set in Unity editor
    public GameObject PiecePrefab, HighlightPrefab;
    SpriteRenderer[] highlights;
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
    Board board;
    int highlightedTile;

    // Start is called before the first frame update
    void Start()
    {
        // Grabs starting position from Board class
        board = new();
        int[] intBoard = board.IntBoard;
        pieces = new(32);
        whiteGraveyard = new(15);
        blackGraveyard = new(15);
        highlights = new SpriteRenderer[64];
        highlightedTile = -1;
        for (int i = 0; i < 64; i++) {
            Vector3 tileCoords = PieceObject.CoordsFromPosition(i);
            // Generates highlight circle for tile
            GameObject highlight = Instantiate(
                HighlightPrefab, // new highlight object inherits from prefab
                tileCoords + new Vector3(0, 0, 1), // Positions highlight at correct position, behind piece
                Quaternion.identity // Starts with no rotation
            );
            highlights[i] = highlight.GetComponent<SpriteRenderer>();
            // Skips piece generation for empty or invalid tiles
            if (intBoard[i] == Piece.Empty || intBoard[i] == -1) {
                continue;
            }
            // Sets piece sprite
            Sprite sprite = pieceSprites[intBoard[i] switch {
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
                PiecePrefab, // New instance inherits from Prefab object 
                tileCoords, // Positions it at correct position
                Quaternion.identity // Starts with no rotation
                );
            // https://docs.unity3d.com/ScriptReference/ScriptableObject.CreateInstance.html
            // Creates PieceObject class to wrap instance GameObject
            PieceObject piece = (PieceObject) ScriptableObject.CreateInstance(typeof(PieceObject));
            piece.SetParams(i, intBoard[i], sprite, instance);
            // Stores PieceObject in array
            pieces.Add(piece);
        }
    }
    void Update() {
        // Unity calls Update() method every frame.

        // Help from https://docs.unity3d.com/ScriptReference/Input.GetMouseButton.html
        // Help from https://docs.unity3d.com/ScriptReference/Input-mousePosition.html
        // Help from https://www.youtube.com/watch?v=5NTmxDSKj-Q
        
        // Mouse position determined, relative to board instead of camera
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition) + cameraOffest;
        int mouseTile = PieceObject.PositionFromCoords(mousePosition);
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
                if (mouseTile > -1) {
                    selectedPiece = null;
                    foreach (PieceObject p in pieces) { // Enhanced for loops in C# use foreach keyword
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
                for (int i = 0; i < 64; i++) {
                    highlights[i].enabled = false;
                }
                if (selectedPiece != null) {
                    foreach (int pos in board.HighlightPositions(selectedPiece.Position)) {
                        highlights[pos].enabled = true;
                    }
                }
                // Debug.Log(selectedPiece + ": " + mouseOffset);
            }
        } else if (mouseDrag) {
            // If mouse released
            mouseDrag = false;
            // Update highlights
            for (int i = 0; i < 64; i++) {
                highlights[i].enabled = false;
            }
            if (mouseTile > -1)
                highlights[mouseTile].enabled = true;
            // Drop piece
            if (selectedPiece != null) {
                // Get move
                Move move = board.CheckMove(selectedPiece.Position, 
                                    PieceObject.PositionFromCoords(mousePosition - mouseOffset));
                if (move.Value == 0) {
                    // If move is not valid
                    selectedPiece.RejectPlacement();
                } else {
                    // If move is valid
                    foreach (PieceObject p in pieces)
                        if (p.Position == move.Dest) {
                            if (Piece.IsColor(board.PieceAt(move.Dest), Piece.White)) {
                                whiteGraveyard.Add(p);
                                p.Kill(Piece.White, whiteGraveyard.Count);
                            } else {
                                blackGraveyard.Add(p);
                                p.Kill(Piece.Black, blackGraveyard.Count);
                            }
                            pieces.Remove(p);
                            break;
                    }
                    selectedPiece.PlacePiece(mousePosition - mouseOffset);
                    board.DoMove(move);
                }
                // Deselect piece
                selectedPiece = null;
            }
        } else {
            // If mouse not down, update highlights
            if (highlightedTile > -1 && mouseTile != highlightedTile) {
                highlights[highlightedTile].enabled = false;
            }
            if (mouseTile > -1) {
                highlights[mouseTile].enabled = true;
            }
            highlightedTile = mouseTile;
        }
        // Update all pieces
        foreach (PieceObject p in pieces) 
            p.Update();
        if (whiteGraveyard.Count > 0)
            whiteGraveyard[^1].Update();
        if (blackGraveyard.Count > 0)
            blackGraveyard[^1].Update();
    }
}
