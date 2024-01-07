using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Text;
using Unity.VisualScripting;

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
    /*
    Modes:
    0: Player v Player
    1: Player v CPU
    2: CPU1 v CPU2
    3: Time CPUs
    4: Player v CPU w/ Time limit
    */
    public int Mode;
    public int SearchDepth;
    public long TimeLimit;
    // Chess AI classes
    public ChessAbstract CPU1, CPU2;
    public string DisplayBitBoard;
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
    static Vector3 dragOffset = new(0, 0, -1);
    static Vector3 highlightOffset = new(0, 0, -1);
    Board board;
    int highlightedTile;
    int doMoveIn;
    bool AITurn;

    // Start is called before the first frame update
    void Start()
    {
        // Grabs starting position from Board class
        // board = new();
        board = new("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - ");
        int[] intBoard = board.IntBoard;
        AITurn = false;
        pieces = new(32);
        whiteGraveyard = new(15);
        blackGraveyard = new(15);
        CPU1.Initialize(true);
        CPU2.Initialize(false);
        highlights = new SpriteRenderer[64];
        highlightedTile = -1;
        doMoveIn = 0;
        for (int i = 0; i < 64; i++) {
            Vector3 tileCoords = PieceObject.CoordsFromPosition(i);
            // Generates highlight circle for tile
            GameObject highlight = Instantiate(
                HighlightPrefab, // new highlight object inherits from prefab
                tileCoords + highlightOffset, // Positions highlight at correct position, behind piece
                Quaternion.identity // Starts with no rotation
            );
            highlights[i] = highlight.GetComponent<SpriteRenderer>();
            // Skips piece generation for empty or invalid tiles
            if (intBoard[i] == Piece.Empty || intBoard[i] == -1) {
                continue;
            }
            // Sets piece sprite
            Sprite sprite = pieceSprites[SpriteIndex(intBoard[i])];
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

    void HandleKeyInputs() 
    {
        /*
        Key Commands:
        Space: Handle asymmetries between game board and internal board
        Backspace: Manually undo move
        M: Do AI move
        P: Print internal board
        T: Print performance test results
        R: Print method durations
        Q: Print total method durations relative to PERFT
        */
        if (Input.GetKeyDown(KeyCode.Space)) {
            HandleAsymmetries(true);
            if (!DisplayBitBoard.Equals(""))
                DrawBitBoard();
        }
        // Use backspace to manually undo moves
        else if (Input.GetKeyDown(KeyCode.Backspace)) {
            board.UndoMove();
            HandleAsymmetries(false);
        }
        else if (Input.GetKeyDown(KeyCode.P)) {
            Debug.Log(board);
            Debug.Log(board.ToFENString());
        }
        else if (Input.GetKeyDown(KeyCode.T)) {
            Debug.Log("Starting Perft Test");
            PerformanceTest perft = new(4, board);
            Dictionary<string, int[]> results = perft.PerfTest(); 
            string resultStr = "Results:";
            foreach (string key in results.Keys) {
                resultStr += "\n" + key + ": [";
                int[] bits = results[key];
                for (int i = 0; i < bits.Count()-1; i++)
                    resultStr += bits[i] + ", ";
                resultStr += bits[^1] + "]";
            }
            Debug.Log(resultStr);
        }
        else if (Input.GetKeyDown(KeyCode.M)) {
            // board.DoMove(search.BestMove(4));
            doMoveIn = 60;
            HandleAsymmetries(false);
        }
        else if (Input.GetKeyDown(KeyCode.R)) {
            string[] funcs = {"nil", "eval", "do", "gen legal", "gen pseudo", "read", "write", "read2", "write2"};
            string results = "";
            foreach (string f in funcs) {
                results += f + " avg: " + CPU1.TimeFunc(f) + " ns\n";
            }
            Debug.Log(results);
        }
        else if (Input.GetKeyDown(KeyCode.Q)) {
            Dictionary<string, int> counts = CPU1.GetPartCounts();
            string results = "";
            foreach (string f in counts.Keys) {
                double time = CPU1.TimeFunc(f) * counts[f] / 1_000_000D;
                results += f + " total: " + time + " ms\n";
            }
            Debug.Log(results);
        }
    }

    int SpriteIndex(int pieceCode) => pieceCode switch 
    {
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
    };

    void KillAt(int index) 
    {
        foreach (PieceObject p in pieces)
            if (p.Position == index) {
                if (Piece.IsColor(board.PieceAt(index), Piece.White)) {
                    whiteGraveyard.Add(p);
                    p.Kill(Piece.White, whiteGraveyard.Count);
                } else {
                    blackGraveyard.Add(p);
                    p.Kill(Piece.Black, blackGraveyard.Count);
                }
                pieces.Remove(p);
                break;
        }
    }

    long TimeMove(bool side, bool doMove) 
    {
        Stopwatch watch = Stopwatch.StartNew();
        Move m;
        if (side) {
            m = CPU1.GetMoveDrafted(board.ToStruct(), SearchDepth);
        } else {
            m = CPU2.GetMoveDrafted(board.ToStruct(), SearchDepth);
        }
        watch.Stop();
        long time1 = watch.ElapsedMilliseconds;
        if (doMove)
            board.DoMove(m);
        return time1;
    }

    void CPUMove() 
    {
        Move m;
        if (Mode == 4) {
            m = CPU1.GetMoveTimed(board.ToStruct(), TimeLimit);
            if (!board.LegalMoves().Contains(m))
                Debug.Log("ILLEGAL " + m.ToString);
        }
        else if (Mode == 3) {
            AITurn = !AITurn;
            long time1 = TimeMove(true, AITurn);
            long time2 = TimeMove(false, !AITurn);
            Debug.Log("CPU1: " + time1 + ", CPU2: " + time2);
            HandleAsymmetries(false);
            return;
        }
        else if (Mode == 2) {
            AITurn = !AITurn;
            m = AITurn ? CPU1.GetMoveDrafted(board.ToStruct(), SearchDepth) : 
                         CPU2.GetMoveDrafted(board.ToStruct(), SearchDepth);
        } 
        else {
            m = CPU1.GetMoveDrafted(board.ToStruct(), SearchDepth);
        }
        board.DoMove(m);
        HandleAsymmetries(false);
        if (Mode == 2) {
            doMoveIn = 60;
        }
    }

    void DrawBitBoard()
    {
        if (DisplayBitBoard.Equals("attack"))
            return;
        ulong bitboard = CPU1.GrabBitBoard(DisplayBitBoard, board.ToStruct());
        for (int i = 0; i < 64; i++) {
            if ((bitboard & (1UL << i)) == 0)
                highlights[i].enabled = false;
            else
                highlights[i].enabled = true;
        }
        // StringBuilder str = new(75);
        // for (int i = 63; i >= 0; i--) {
        //     str.Append((bitboard & (1UL << i)) == 0 ? '0' : '1');
        //     if (i % 8 == 0) str.Append('\n');
        // }
        // Debug.Log("Bit Board: " + DisplayBitBoard + '\n' + str.ToString());
    }

    void Update() 
    {
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
                    selectedPiece.SetPosition(mousePosition - mouseOffset + dragOffset);
                }
            } else {
                // If mouse is down but not yet dragging
                mouseDrag = true;
                // Determine which tile the mouse is hovering over
                if (mouseTile > -1) {
                    selectedPiece = null;
                    foreach (PieceObject p in pieces) { 
                        if (p.Position == mouseTile) {
                            // If mouse is hovering over a piece
                            // Select piece
                            selectedPiece = p;
                            // Determine difference between mouse and piece position
                            mouseOffset = mousePosition - p.Coords;
                            // Debug.Log(selectedPiece.Position);
                            break;
                        }
                    }
                }
                for (int i = 0; i < 64; i++) {
                    highlights[i].enabled = false;
                }
                if (selectedPiece != null) {
                    if (DisplayBitBoard.Equals("attack")) {
                        ulong bitboard = CPU1.GrabAttackBoard(selectedPiece.Position, board.ToStruct());
                        for (int i = 0; i < 64; i++) {
                            if ((bitboard & (1UL << i)) == 0)
                                highlights[i].enabled = false;
                            else
                                highlights[i].enabled = true;
                        }
                    }
                    else {   
                        foreach (int pos in board.HighlightPositions(selectedPiece.Position)) {
                            highlights[pos].enabled = true;
                        }
                    }
                }
                // Debug.Log(selectedPiece + ": " + mouseOffset);
            }
        } else if (mouseDrag) {
            // If mouse released
            mouseDrag = false;
            // Update highlights
            if (DisplayBitBoard.Equals("")) {
                for (int i = 0; i < 64; i++) {
                    highlights[i].enabled = false;
                }
                if (mouseTile > -1)
                    highlights[mouseTile].enabled = true;
            } else {
                DrawBitBoard();
            }
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
                    if (move.Type == Move.TypeCastle) {
                        // If move is a castle, do corresponding rook move
                        Move rookMove = move.CastlePartnerMove;
                        foreach (PieceObject p in pieces)
                            if (p.Position == rookMove.Start) {
                                p.MoveTo(rookMove.Dest);
                                break;
                            }
                    }
                    else if (move.Type == Move.TypeEnPassant) {
                        // If move is en passant, kill passed pawn
                        KillAt(move.DestCol + move.StartRow * 8);
                    } 
                    else {
                        // If move is a capture, kill captured piece
                        KillAt(move.Dest);
                        if (move.IsPromotion) {
                            int promotedPiece = Piece.Color(board.IntBoard[move.Start]) | move.PromotionPieceType;
                            selectedPiece.ChangeSprite(pieceSprites[SpriteIndex(promotedPiece)]);
                            selectedPiece.PieceCode = promotedPiece;
                        }
                    }
                    selectedPiece.PlacePiece(mousePosition - mouseOffset);
                    board.DoMove(move);
                    if (!DisplayBitBoard.Equals("")) {
                        DrawBitBoard();
                    }
                    doMoveIn = 60;
                }
                // Deselect piece
                selectedPiece = null;
            }
        } else {
            // If mouse not down, update highlights
            if (DisplayBitBoard.Equals("")) {
                if (highlightedTile > -1 && mouseTile != highlightedTile) {
                    highlights[highlightedTile].enabled = false;
                }
                if (mouseTile > -1) {
                    highlights[mouseTile].enabled = true;
                }
                highlightedTile = mouseTile;
            }
        }
        if (doMoveIn > 0) {
            doMoveIn--;
            if (doMoveIn == 0 && Mode > 0) {
                CPUMove();
            }
        }

        HandleKeyInputs();

        // Update all pieces
        foreach (PieceObject p in pieces) 
            p.Update();
        if (whiteGraveyard.Count > 0)
            whiteGraveyard[^1].Update();
        if (blackGraveyard.Count > 0)
            blackGraveyard[^1].Update();
    }

    void HandleAsymmetries(bool debug) {
        List<PieceObject> gameAsymmetries = new(4);
        List<Tuple<int, int>> boardAsymmetries = new(4);
        int[] gameBoard = new int[64];
        int[] intBoard = board.IntBoard;
        foreach (PieceObject p in pieces) {
            gameBoard[p.Position] = p.PieceCode;
            if (intBoard[p.Position] != p.PieceCode) {
                gameAsymmetries.Add(p);
            }
        }
        for (int i = 0; i < 64; i++) {
            if (intBoard[i] != gameBoard[i] && intBoard[i] > 0)
                boardAsymmetries.Add(new(intBoard[i], i));
        }
        while (gameAsymmetries.Count > 0) {
            int boardAsymIndex = -1;
            for (int i = 0; i < boardAsymmetries.Count; i++) {
                if (boardAsymmetries[i].Item1 == gameAsymmetries[0].PieceCode) {
                    boardAsymIndex = i;
                    break;
                }
            }
            if (boardAsymIndex == -1) {
                PieceObject killThisOne = gameAsymmetries[0];
                if (debug) Debug.Log("Piece " + killThisOne + " at " + killThisOne.Position + " killed.");
                if (Piece.IsColor(killThisOne.PieceCode, Piece.White)) {
                    whiteGraveyard.Add(killThisOne);
                    killThisOne.Kill(Piece.White, whiteGraveyard.Count);
                } else {
                    blackGraveyard.Add(killThisOne);
                    killThisOne.Kill(Piece.Black, blackGraveyard.Count);
                }
                pieces.Remove(killThisOne);
            } else {
                if (debug)
                    Debug.Log("Piece " + gameAsymmetries[0] + " at " + gameAsymmetries[0].Position + 
                              " moved to " + boardAsymmetries[boardAsymIndex].Item2 + ".");
                gameAsymmetries[0].MoveTo(boardAsymmetries[boardAsymIndex].Item2);
                boardAsymmetries.RemoveAt(boardAsymIndex);
            }
            gameAsymmetries.RemoveAt(0);
        }
        while (boardAsymmetries.Count > 0) {
            bool revived = false;
            int pieceCode = boardAsymmetries[0].Item1;
            int index = boardAsymmetries[0].Item2;
            foreach (PieceObject corpse in Piece.IsColor(pieceCode, Piece.White) ? whiteGraveyard : blackGraveyard) {
                if (corpse.PieceCode == pieceCode) {
                    corpse.MoveTo(index);
                    whiteGraveyard.Remove(corpse);
                    blackGraveyard.Remove(corpse);
                    pieces.Add(corpse);
                    revived = true;
                    if (debug) Debug.Log("Piece " + corpse + " revived at " + index + ".");
                    break;
                }
            }
            if (!revived) {
                if (whiteGraveyard.Count > 0) {
                    whiteGraveyard[0].MoveTo(index);
                    whiteGraveyard[0].PieceCode = pieceCode;
                    whiteGraveyard[0].ChangeSprite(pieceSprites[SpriteIndex(pieceCode)]);
                    pieces.Add(whiteGraveyard[0]);
                    if (debug) Debug.Log("Piece " + whiteGraveyard[0] + " created at " + index + ".");
                    whiteGraveyard.RemoveAt(0);
                } 
                else if (blackGraveyard.Count > 0) {
                    blackGraveyard[0].MoveTo(index);
                    blackGraveyard[0].PieceCode = pieceCode;
                    blackGraveyard[0].ChangeSprite(pieceSprites[SpriteIndex(pieceCode)]);
                    pieces.Add(blackGraveyard[0]);
                    if (debug) Debug.Log("Piece " + blackGraveyard[0] + " created at " + index + ".");
                    blackGraveyard.RemoveAt(0);
                }
                else {
                    Debug.Log("Graveyard empty, asymmetry of " + Piece.ToString(pieceCode) + 
                              " at " + index + " could not be resolved.");
                }
            }
            boardAsymmetries.RemoveAt(0);
        }
    }
}
