using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Linq;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

sealed class Board : IEquatable<Board>
{
    // Stores letters that represent different files
    public static readonly char[] LetterCodes = new char[] {'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'};
    // Stores integer representation of board.
    public int[] IntBoard { get; }
    // Stores information about whose turn it is
    public bool WhiteToMove { get; set; }
    public int ColorToMove { get; set; }
    public int OpponentColor { get; set; }
    // Stores information about current board state
    // Bit-wise Format: MMMMMMMMMMMMMMHHHHHHLLLLPPPPPCCCC, 
    // * M = Move
    // * H = halfmoveClock, 
    // * L = pawnLeapFile, 
    // * P = capturedPiece, 
    // * C = castlingRights
    public uint StateData;
    // Contains history of the state data from all previous states (moves) in a game
    public Stack<uint> StateHistory { get; }
    public int Fullmove;
    // Current state data:
    int halfmoveClock;
    int pawnLeapFile; // 0 is A File, 7 is H file, 8 is no pawn leap
    int capturedPiece;
    int castlingRights; // Format: KQkq
    // castlingRights bitwise masks:
    const int WKCastleMask = 0b1000; // White Kingside Castle Mask
    const int WQCastleMask = 0b0100; // White Queenside Castle Mask
    const int BKCastleMask = 0b0010; // Black Kingside Castle Mask
    const int BQCastleMask = 0b0001; // Black Queenside Castle Mask

    // CastlePathMasks:
    const ulong WQPathMask = 0b111UL << 2;
    const ulong WKPathMask = 0b111UL << 4;
    const ulong BQPathMask = 0b111UL << 58;
    const ulong BKPathMask = 0b111UL << 60;
    // King indices:
    int kingIndex, wkIndex, bkIndex;
    // Contains all the ways a chess game can end, plus "Running" for a game that has not ended.
    public enum Status
    {
        Running,
        WinByCheckmate,
        WinByTimeout,
        DrawByInsufficientMaterial,
        DrawByTimeoutVsInsufficientMaterial,
        DrawByStalemate,
        DrawBy50MoveRule,
        DrawByRepitition,
        WinByResignation,
        DrawByAgreement,
        Terminated
    }
    // List of legal moves in current position
    List<Move> moveList; 

    public Board()
    {
        // Sets all variables for new starting game
        IntBoard = StartingBoard();
        wkIndex = kingIndex = 4;
        bkIndex = 60;
        moveList = new List<Move>(50);
        WhiteToMove = true;
        ColorToMove = Piece.White;
        OpponentColor = Piece.Black;
        castlingRights = 0b1111;
        pawnLeapFile = 8;
        halfmoveClock = 0;
        capturedPiece = 0;
        StateData = (uint)(castlingRights | (capturedPiece << 4) | (pawnLeapFile << 9) | (halfmoveClock << 13));
        StateHistory = new Stack<uint>(70);
        StateHistory.Push(StateData);
    }

    public Board(int[] IntBoard, int wkIndex, int bkIndex, int kingIndex, bool WhiteToMove, int castlingRights, int pawnLeapFile, int halfmoveClock, int capturedPiece) 
    {
        this.IntBoard = (int[])IntBoard.Clone();
        moveList = new List<Move>(50);
        
        this.wkIndex = wkIndex;
        this.bkIndex = bkIndex;
        this.kingIndex = kingIndex;

        this.WhiteToMove = WhiteToMove;
        if (WhiteToMove) {
            ColorToMove = Piece.White;
            OpponentColor = Piece.Black; 
        }
        else {
            ColorToMove = Piece.Black;
            OpponentColor = Piece.White;
        }
        
        this.castlingRights = castlingRights;
        this.pawnLeapFile = pawnLeapFile;
        this.halfmoveClock = halfmoveClock;
        this.capturedPiece = capturedPiece;

        StateData = (uint)(castlingRights | (capturedPiece << 4) | (pawnLeapFile << 9) | (halfmoveClock << 13));
        StateHistory = new Stack<uint>(70);
        StateHistory.Push(StateData);
    }

    public Board(int[] boardArray) : this() 
    {
        /*
        In C#, if a method contains another method in its signature (in this case "this()),
        The method inherits from another. In this case, that means all code from first constructor
        is ran before this one, and this can access all local variables from the previous
        */
        // Starts game from inputted board, no state history (used for training and puzzles)
        IntBoard = boardArray;
        for (int i = 0; i < 64; i++) {
            if (boardArray[i] == (Piece.White | Piece.King))
                kingIndex = wkIndex = i;
            else if (boardArray[i] == (Piece.Black | Piece.King))
                bkIndex = i;
        }
    }

    public Board(string FEN) 
    {
        IntBoard = new int[64];
        int progress = ReadFEN(FEN);
        if (progress < 1) {
            IntBoard = StartingBoard();
        }
        if (progress < 2) {
            WhiteToMove = true;
            ColorToMove = Piece.White;
            OpponentColor = Piece.Black;
        }
        if (progress < 3) {
            castlingRights = 0b1111;
        }
        if (progress < 4) {
            pawnLeapFile = 8;
        }
        if (progress < 5) {
            halfmoveClock = 0;
        }
        if (progress < 6) {
            Fullmove = 0;
        }
        kingIndex = ColorToMove == Piece.White ? wkIndex : bkIndex;
        StateData = (uint)(castlingRights | (capturedPiece << 4) | (pawnLeapFile << 9) | (halfmoveClock << 13));
        StateHistory = new Stack<uint>(70);
        StateHistory.Push(StateData);
    }

    Board Clone() => new(IntBoard, wkIndex, bkIndex, kingIndex, WhiteToMove, castlingRights, pawnLeapFile, halfmoveClock, capturedPiece);

    public bool Equals(Board other) => 
        IntBoard == other.IntBoard &&
        wkIndex == other.wkIndex &&
        bkIndex == other.bkIndex &&
        kingIndex == other.kingIndex &&
        WhiteToMove == other.WhiteToMove &&
        castlingRights == other.castlingRights &&
        pawnLeapFile == other.pawnLeapFile &&
        halfmoveClock == other.halfmoveClock &&
        capturedPiece == other.capturedPiece;

    void LogAsymmetries(Board other) {
        if (!Enumerable.SequenceEqual(IntBoard, other.IntBoard)) {
            Debug.Log("IntBoard\nThis\n" + ToString() + "\n\n Other\n" + other);
        }
        if (wkIndex != other.wkIndex) Debug.Log("wkIndex");
        if (bkIndex != other.bkIndex) Debug.Log("bkIndex");
        if (kingIndex != other.kingIndex) {
            Debug.Log("kingIndex\nThis: " + kingIndex + "\nOther: " + other.kingIndex +
                    "\nthis.wk: " + wkIndex + ", this.bk: " + bkIndex);
        }
        if (WhiteToMove != other.WhiteToMove) Debug.Log("WhiteToMove");
        if (castlingRights != other.castlingRights) Debug.Log("castlingRights");
        if (pawnLeapFile != other.pawnLeapFile) {
            Debug.Log("pawnLeapFile\nThis: " + pawnLeapFile + "\nOther: " + other.pawnLeapFile);
        }
        if (halfmoveClock != other.halfmoveClock) Debug.Log("halfmoveClock");
        if (capturedPiece != other.capturedPiece) {
            Debug.Log("capturedPiece\nThis: " + Piece.ToString(capturedPiece) + "\nOther: " + Piece.ToString(other.capturedPiece));
        }
    }

    int ReadFEN(string FEN) 
    {
        // Sets board information from FEN string
        // Returns how much progress it was able to make (0 if formatted incorrectly)
        // Constructor will handle whatever this method can't
        int fenIndex = 0;
        int boardIndex = 0;
        int tries = 0;

        // Segment 1: Board
        while (boardIndex < 64) {
            if (tries++ > 72) {
                Debug.Log("FEN failed on board (infinite loop)");
                return 0;
            }
            char c = FEN[fenIndex++];
            if (('A' <= c && c <= 'Z') || ('a' <= c && c <= 'z')) {
                int p = Piece.FENChar(c);
                int index = (7-(boardIndex/8))*8+(boardIndex%8);
                IntBoard[index] = p;
                if (p == (Piece.White | Piece.King))
                    wkIndex = index;
                else if (p == (Piece.Black | Piece.King))
                    bkIndex = index;
                boardIndex++;
            } 
            else if ('1' <= c && c <= '8') {
                boardIndex += c - '0';
            }
            else if (c == '/') {
                if (boardIndex % 8 > 0) {
                    Debug.Log("FEN failed on board (unexpected '/')");
                    return 0; 
                }
            }
            else {
                Debug.Log("FEN failed on board (unknown char)");
                return 0;
            }
        }

        // Segment 2: Active Player
        fenIndex++;
        if (fenIndex >= FEN.Length) {
            Debug.Log("FEN failed on active player (incomplete FEN string)");
            return 1;
        }
        if (FEN[fenIndex] == 'w') {
            WhiteToMove = true;
            ColorToMove = Piece.White;
            OpponentColor = Piece.Black;
        }
        else if (FEN[fenIndex] == 'b') {
            WhiteToMove = false;
            ColorToMove = Piece.Black;
            OpponentColor = Piece.White;
        }
        else {
            Debug.Log("FEN failed on active player (unknown char))");
            return 1;
        }

        // Segment 3: Castling Rights
        castlingRights = 0;
        fenIndex += 2;
        tries = 0;
        if (fenIndex >= FEN.Length) {
            Debug.Log("FEN failed on castle rights (incomplete FEN string)");
            return 2;
        }
        if (FEN[fenIndex] == ' ') {
            Debug.Log("FEN failed on castle rights (unexpected ' ')");
            return 2;
        }
        while (FEN[fenIndex] != ' ') {
            if (tries++ > 4) {
                Debug.Log("FEN failed on castle rights (infinite loop)");
                return 2;
            }
            if (!"KQkq-".Contains(FEN[fenIndex])) {
                Debug.Log("FEN failed on castle rights (unexpected char)");
                return 2;
            }
            castlingRights |= FEN[fenIndex] switch {
                'k' => BKCastleMask,
                'K' => WKCastleMask,
                'q' => BQCastleMask,
                'Q' => WQCastleMask,
                '-' => 0,
                _ => -1
            };
            fenIndex++;
        }

        // Segment 4: Pawn Leap File
        fenIndex++;
        if (fenIndex >= FEN.Length) {
            Debug.Log("FEN failed on pawn leap file (incomplete FEN string)");
            return 3;
        }
        char leapChar = FEN[fenIndex];
        if (leapChar == '-') {
            pawnLeapFile = 8;
            fenIndex += 2;
        } else {
            if (leapChar > 'Z') 
                leapChar -= (char)32; 
            if (leapChar < 'A' || leapChar > 'Z') {
                Debug.Log("FEN failed on pawn leap file (unexpected char)");
                return 3;
            }
            pawnLeapFile = leapChar - 'A';
            fenIndex += 3;
        }

        // Segment 5: Half-move clock
        if (fenIndex + 1 >= FEN.Length) {
            // Debug.Log("FEN failed on half-move clock (incomplete FEN string)");
            return 4;
        }
        if (FEN[fenIndex] < '0' || FEN[fenIndex] > '9') {
            // Debug.Log("FEN failed on half-move clock (unexpected char)");
            return 4;
        }
        else if (FEN[fenIndex + 1] == ' ') {
            halfmoveClock = FEN[fenIndex] - '0';
            fenIndex += 2;
        }
        else if (FEN[fenIndex + 1] < '0' || FEN[fenIndex + 1] > '9') {
            // Debug.Log("FEN failed on half-move clock (unexpected char)");
            return 4;
        }
        else {
            halfmoveClock = 10 * (FEN[fenIndex] - '0') + FEN[fenIndex + 1] - '0'; 
            fenIndex += 3;
        }

        // Segment 6: Fullmove number
        if (fenIndex >= FEN.Length) {
            // Debug.Log("FEN failed on full-move number (incomplete FEN string)");
            return 5;
        }
        Fullmove = 0;
        while (fenIndex < FEN.Length) {
            if (tries++ > 7) {
                // Debug.Log("FEN failed on full-move number (infinite loop)");
                return 5;
            } 
            Fullmove *= 10;
            if (FEN[fenIndex] < '0' || FEN[fenIndex] > '9') {
                // Debug.Log("FEN failed on full-move number (unexpected char)");
                return 5;
            }
            Fullmove += FEN[fenIndex] - '0';
        }

        return 6;
    }
    
    public static int[] StartingBoard() 
    {
        // Returns initial board position
        int[] b = new int[64];
        b[0] = b[7] = Piece.White | Piece.Rook;
        b[1] = b[6] = Piece.White | Piece.Knight;
        b[2] = b[5] = Piece.White | Piece.Bishop;
        b[3] = Piece.White | Piece.Queen;
        b[4] = Piece.White | Piece.King;
        b[8] = b[9] = b[10] = b[11] = b[12] = b[13] = b[14] = b[15] = Piece.WhitePawn;

        b[56] = b[63] = Piece.Black | Piece.Rook;
        b[57] = b[62] = Piece.Black | Piece.Knight;
        b[58] = b[61] = Piece.Black | Piece.Bishop;
        b[59] = Piece.Black | Piece.Queen;
        b[60] = Piece.Black | Piece.King;
        b[48] = b[49] = b[50] = b[51] = b[52] = b[53] = b[54] = b[55] = Piece.BlackPawn;

        return b;
    }

    public static Board BoardFromIntArray(int[] boardArray) 
    {
        // Creates Board object from int array representation
        // Blank state history
        return new Board(boardArray);
    }

    public Status GameStatus() 
    {
        // Checks if a game is finished.
        /*
        Current problems with method:
          * Does not check repetition, timeout, or insufficient material
        */
        // If halfmove clock exceeds 50 (meaning no significant moves have occured in the past 25 moves)
        // Then return a Draw
        if (halfmoveClock >= 50)
            return Status.DrawBy50MoveRule;
        // Get all moves in the current position, including moves that result in check
        PseudoLegalMoves();
        Move[] moves = new Move[moveList.Count];
        moveList.CopyTo(moves);
        bool isThreatened = IsCheck();
        bool canMove = CullIllegalMoves(moves.ToList()).Count > 0;
        if (!canMove) {
            if (isThreatened)
                return Status.WinByCheckmate;
            else
                return Status.DrawByStalemate;
        }
        // If no previous end conditions were met, game is still running
        return Status.Running;
    }
    public bool IsCheck() 
    {
        // Checks if king is currently in check
        PseudoLegalMoves();
        foreach (Move move in moveList) {
            if (move.Dest == (OpponentColor == Piece.White ? wkIndex : bkIndex))
                return true;
        }
        return false;
    }

    public void DoMove(Move move)
    {
        // Unpack move
        int start = move.Start;
        int dest = move.Dest;
        int type = move.Type;

        int movingPiece = Piece.Type(IntBoard[start]);
        int movingColor = Piece.Color(IntBoard[start]);

        // Update king index
        if (movingPiece == Piece.King) {
            if (movingColor == Piece.White) wkIndex = dest;
            else bkIndex = dest;
        }

        // Unpack state
        castlingRights = (int)(StateData & 0b1111);
        pawnLeapFile = 8;
        halfmoveClock = (int)(((StateData >> 13) & 0b111111) + 1);

        // Handle pawn promotion moves
        if (move.IsPromotion)
        {
            halfmoveClock = 0;
            IntBoard[start] = 0;
            capturedPiece = IntBoard[dest];
            switch (type)
            {
                case Move.TypePromoteToQueen:
                    IntBoard[dest] = Piece.Queen | movingColor;
                    break;
                case Move.TypePromoteToKnight:
                    IntBoard[dest] = Piece.Knight | movingColor;
                    break;
                case Move.TypePromoteToBishop:
                    IntBoard[dest] = Piece.Bishop | movingColor;
                    break;
                case Move.TypePromoteToRook:
                    IntBoard[dest] = Piece.Rook | movingColor;
                    break;
            }
        }
        // Handles all other moves
        else switch (type)
        {
            // Castle moves
            case Move.TypeCastle:
                capturedPiece = 0;
                IntBoard[dest] = IntBoard[start];
                IntBoard[start] = 0;
                switch (dest)
                {
                    case 2:
                        IntBoard[0] = 0;
                        IntBoard[3] = Piece.WhiteRook;
                        castlingRights &= 0b0011;
                        break;
                    case 6:
                        IntBoard[7] = 0;
                        IntBoard[5] = Piece.WhiteRook;
                        castlingRights &= 0b0011;
                        break;
                    case 58:
                        IntBoard[56] = 0;
                        IntBoard[59] = Piece.BlackRook;
                        castlingRights &= 0b1100;
                        break;
                    case 62:
                        IntBoard[63] = 0;
                        IntBoard[61] = Piece.BlackRook;
                        castlingRights &= 0b1100;
                        break;
                }
                break;
            // En passant captures
            case Move.TypeEnPassant:
                if ((dest >> 3) == 0b010)
                {
                    capturedPiece = Piece.WhitePawn;
                    IntBoard[dest + 8] = 0;
                }
                else
                {
                    capturedPiece = Piece.BlackPawn;
                    IntBoard[dest - 8] = 0;
                }
                IntBoard[dest] = IntBoard[start];
                IntBoard[start] = 0;
                break;
            // Pawn moving forward two spaces
            case Move.TypePawnLeap:
                halfmoveClock = 0;
                pawnLeapFile = start & 0b111; // Saves file state data so that it can be captured in future en passant
                capturedPiece = 0;
                IntBoard[dest] = IntBoard[start];
                IntBoard[start] = 0;
                break;
            // All other moves
            default:
                if (movingPiece == Piece.King)
                {
                    // King moves remove castling rights
                    if (movingColor == Piece.White) castlingRights &= 0b0011;
                    else castlingRights &= 0b1100;
                }
                else if (movingPiece == Piece.Rook)
                {
                    // Rook moves remove castling rights on their side
                    switch (start)
                    {
                        case 0:
                            castlingRights &= 0b0111;
                            break;
                        case 7:
                            castlingRights &= 0b1011;
                            break;
                        case 56:
                            castlingRights &= 0b1101;
                            break;
                        case 63:
                            castlingRights &= 0b1110;
                            break;
                    }
                }
                else if (movingPiece == Piece.Pawn) {
                    halfmoveClock = 0; // Reset halfmove clock if pawn is moved
                }
                capturedPiece = IntBoard[dest];
                IntBoard[dest] = IntBoard[start];
                IntBoard[start] = 0;
                break;
        }

        if (capturedPiece != 0) {
            halfmoveClock = 0; // Reset halfmove clock if piece is captured 
        } 

        // Repack state data
        // Format: MMMMMMMMMMMMMMHHHHHHLLLLPPPPPCCCC, 
        // M = Move, H = halfmoveClock, L = pawnLeapFile, P = capturedPiece, C = castlingRights
        int shortType = type switch {
            Move.TypeEnPassant => 1,
            Move.TypeCastle => 2,
            4 | 5 | 6 | 7 => 3,
            _ => 0
        };
        int isSpecialPawnMove = ((type >= 4 && type <= 7) || type == 2) ? 1 : 0;
        StateData = (uint)castlingRights | ((uint)capturedPiece << 4) | ((uint)pawnLeapFile << 9) | ((uint)halfmoveClock << 13) | ((uint)(isSpecialPawnMove << 12 | dest << 6 | start) << 19);
        // Save state data
        StateHistory.Push(StateData);

        SwapTurn();
    }

    public void UndoMove()
    {
        // Unpack state data
        StateData = StateHistory.Pop();

        castlingRights = (int)StateData & 0b1111;
        capturedPiece = (int)(StateData >> 4) & 0b11111;
        pawnLeapFile = (int)(StateData >> 9) & 0b1111;
        halfmoveClock = (int)(StateData >> 13) & 0b111111;
        int move = (int)(StateData >> 19);

        // Unpack move
        int start = Move.StatStart(move);
        int dest = Move.StatDest(move);
        int isSpecialPawnMove = Move.StatType(move);
        int movingPiece = Piece.Type(IntBoard[dest]);
        int movingColor = Piece.Color(IntBoard[dest]);


        // Handles king index
        if (movingPiece == Piece.King) {
            if (movingColor == Piece.White)
                wkIndex = start;
            else
                bkIndex = start;
        }


        // Undoes Special Pawn moves
        if (isSpecialPawnMove == 1) {
            if ((dest >> 3) % 7 == 0) {
                // Undoes promotion
                IntBoard[start] = Piece.Pawn | Piece.Color(IntBoard[dest]);
                IntBoard[dest] = capturedPiece;
            }
            else {
                // Undoes En Passant
                if ((dest >> 3) == 0b010) 
                    IntBoard[dest + 8] = Piece.WhitePawn;
                else 
                    IntBoard[dest - 8] = Piece.BlackPawn;
                IntBoard[start] = IntBoard[dest];
                IntBoard[dest] = 0;
            }
        }
        // Undoes Castle
        else if (Piece.IsType(IntBoard[dest], Piece.King) && Math.Abs(start - dest) == 2) {
            capturedPiece = 0;
            IntBoard[start] = IntBoard[dest];
            IntBoard[dest] = 0;
            switch (dest)
            {
                case 2:
                    IntBoard[0] = Piece.WhiteRook;
                    IntBoard[3] = 0;
                    break;
                case 6:
                    IntBoard[7] = Piece.WhiteRook;
                    IntBoard[5] = 0;
                    break;
                case 58:
                    IntBoard[56] = Piece.BlackRook;
                    IntBoard[59] = 0;
                    break;
                case 62:
                    IntBoard[63] = Piece.BlackRook;
                    IntBoard[61] = 0;
                    break;
            }
        }
        // Undoes all other moves
        else {
            IntBoard[start] = IntBoard[dest];
            IntBoard[dest] = capturedPiece;
        }

        SwapTurn();

        StateData = StateHistory.Peek();

        castlingRights = (int)StateData & 0b1111;
        capturedPiece = (int)(StateData >> 4) & 0b11111;
        pawnLeapFile = (int)(StateData >> 9) & 0b1111;
        halfmoveClock = (int)(StateData >> 13) & 0b111111;
    }

    void SwapTurn() {
        WhiteToMove = !WhiteToMove;
        if (WhiteToMove)
        {
            ColorToMove = Piece.White;
            OpponentColor = Piece.Black;
            kingIndex = wkIndex;
        }
        else
        {
            ColorToMove = Piece.Black;
            OpponentColor = Piece.White;
            kingIndex = bkIndex;
        }
    }

    public Move CheckMove(int start, int dest) 
    {
        moveList = CullIllegalMoves(PseudoLegalMoves(start).ToList());
        foreach (Move m in moveList) {
            if (m.Start == start && m.Dest == dest) {
                return m;
            }
        }
        return new Move(0, 0, 0);
    }
    public List<Move> PseudoLegalMoves()
    {
        moveList = new List<Move>(50);

        for (int pos = 0; pos < 64; pos++)
            if (Piece.IsColor(IntBoard[pos], ColorToMove))
                GenMoves(pos);

        return moveList;
    }

    public List<Move> PseudoLegalMoves(int pos) 
    {
        moveList = new List<Move>(28);

        if (Piece.IsColor(IntBoard[pos], ColorToMove))
            GenMoves(pos);

        return moveList;
    }


    public int[] HighlightPositions(int start) 
    {
        // Debug.Log("wk: " + wkIndex + ", bk: " + bkIndex);
        moveList = CullIllegalMoves(PseudoLegalMoves(start).ToList());
        int[] dests = new int[moveList.Count];
        for (int i = 0; i < moveList.Count; i++)
            dests[i] = moveList[i].Dest;
        return dests;
    }

    public List<Move> CullIllegalMoves(List<Move> moves) 
    {
        Board original = Clone();
        List<Move> legalMoves = new(moves.Count);
        foreach (Move m in moves) {
            if (m.Type == Move.TypeCastle) {
                // Cull castles if king is in check or passes over a threatened square
                SwapTurn();
                ulong killMap = DrawKillMap(PseudoLegalMoves());
                killMap &= m.Dest switch {
                    2 => WQPathMask,
                    6 => WKPathMask,
                    58 => BQPathMask,
                    62 => BKPathMask,
                    _ => 0UL
                };
                SwapTurn();
                if (killMap > 0UL) {
                    continue;
                }
            }
            DoMove(m);
            if (!IsCheck())
                legalMoves.Add(m);
            UndoMove();
        }
        LogAsymmetries(original);
        return legalMoves;
    }

    public ulong DrawKillMap(List<Move> moves) {
        ulong killMap = 0UL;
        foreach (Move m in moves) {
            killMap |= 1UL << m.Dest;
        }
        return killMap;
    }

    void GenMoves(int pos) 
    {
        if (Piece.CanSlide(IntBoard[pos]))
                    SlidingMoves(pos);
        else switch (Piece.Type(IntBoard[pos]))
            {
                case Piece.King:
                    KingMoves(pos);
                    break;
                case Piece.Pawn:
                    PawnMoves(pos);
                    break;
                case Piece.Knight:
                    KnightMoves(pos);
                    break;
            }
    }
/*
    public bool CheckMoveLegality(Move move) 
    {
        int[] boardCopy = board.Clone();
        DoMove(move);
        int kingIndex = -1;
        for (int i = 0; i < 64; i++) {
            if (board[i] = Piece.King & OpponentColor)
                kingIndex = i;
        }
        
        Move[] moves = PseudoLegalMoves();

    }
*/
    

    void TryAdd(int pos1, int pos2, int flag)
    {
        // if (flag == Move.TypeEnPassant) Debug.Log("En Passant " + pos1 + " --> " + pos2);
        if (pos1 < 0 || pos1 > 63 || pos2 < 0 || pos2 > 63) return;
        if (Piece.IsColor(IntBoard[pos2], ColorToMove)) return;
        if (flag == -1 && (pos2 >> 3) % 7 == 0) {
            moveList.Add(new Move(pos1, pos2, Move.TypePromoteToQueen));
            moveList.Add(new Move(pos1, pos2, Move.TypePromoteToKnight));
        }
        else {
            if (flag == -1) flag++;
            if (Piece.IsColor(IntBoard[pos2], OpponentColor) && flag == Move.TypeNormal)
                flag = Piece.Type(IntBoard[pos2]) + 8;
            moveList.Add(new Move(pos1, pos2, flag));
        }
    }

    void SlidingMoves(int pos)
    {
        if (Piece.CanSlideStraight(IntBoard[pos]))
        {
            SlidePiece(pos, 1, 0);
            SlidePiece(pos, -1, 0);
            SlidePiece(pos, 0, 1);
            SlidePiece(pos, 0, -1);
        }
        if (Piece.CanSlideDiagonal(IntBoard[pos]))
        {
            SlidePiece(pos, 1, 1);
            SlidePiece(pos, 1, -1);
            SlidePiece(pos, -1, 1);
            SlidePiece(pos, -1, -1);
        }
    }

    public void UnitTest(int test) 
    {
        switch (test) {
            case 0:

                break;
        }
    }

    void SlidePiece(int pos1, int offsetX, int offsetY)
    {
        int color = Piece.Color(IntBoard[pos1]);
        /*
        int range = Math.Min(
            ((pos1 & 0b111) * -offsetX + 7) % 7 + (((offsetX & 0b1) ^ 0b1) << 3),
            ((pos1 >> 3) * -offsetY + 7) % 7 + (((offsetY & 0b1) ^ 0b1) << 3)
            );
        */
        int rangeX = offsetX switch {
            -1 => pos1 & 0b111,
            1 => 7 - (pos1 & 0b111),
            _ => 8
        };
        int rangeY = offsetY switch {
            -1 => pos1 >> 3,
            1 => 7 - (pos1 >> 3),
            _ => 8
        };
        int range = Math.Min(rangeX, rangeY);
        int offset = offsetX + (offsetY * 8);
        int pos2 = pos1;
        for (int i = 0; i < range; i++)
        {
            pos2 += offset;
            if (IntBoard[pos2] == 0) TryAdd(pos1, pos2, 0);
            else {
                if (!Piece.IsColor(IntBoard[pos2], color)) TryAdd(pos1, pos2, 0);
                break;
            }
        }
    }

    void PawnMoves(int pos)
    {
        if (WhiteToMove)
        {
            if (IntBoard[pos + 8] == 0) { 
                TryAdd(pos, pos + 8, -1); // Push up one
                if (pos >> 3 == 1 && IntBoard[pos + 16] == 0)
                    TryAdd(pos, pos + 16, Move.TypePawnLeap); // Push up two
            }
            if (Piece.IsColor(IntBoard[pos + 7], OpponentColor)) {
                TryAdd(pos, pos + 7, -1); // Pawn attack up-left
            } else if ((pos-1)%8 == pawnLeapFile && pos/8 == 4) {
                TryAdd(pos, pos + 7, Move.TypeEnPassant); // Pawn en passant capture left
            }
            if (pos + 9 < 64 && Piece.IsColor(IntBoard[pos + 9], OpponentColor)) { 
                TryAdd(pos, pos + 9, -1); // Pawn attack up-right
            } else if ((pos+1)%8 == pawnLeapFile && pos/8 == 4) {
                TryAdd(pos, pos + 9, Move.TypeEnPassant); // Pawn en passant capture right
            }
        }
        else
        {
            // Debug.Log("moving black pawn");
            if (IntBoard[pos - 8] == 0)
            {
                TryAdd(pos, pos - 8, -1);
                if (pos >> 3 == 6 && IntBoard[pos - 16] == 0)
                    TryAdd(pos, pos - 16, 3);
            }
            if (Piece.IsColor(IntBoard[pos - 7], OpponentColor)) {
                TryAdd(pos, pos - 7, -1);
            } 
            else if ((pos+1)%8 == pawnLeapFile && pos/8 == 3) {
                TryAdd(pos, pos - 7, Move.TypeEnPassant);
            }
            if (pos - 9 > -1 && Piece.IsColor(IntBoard[pos - 9], OpponentColor)) {
                TryAdd(pos, pos - 9, -1);
            } 
            else if ((pos-1)%8 == pawnLeapFile && pos/8 == 3) {
                TryAdd(pos, pos - 9, Move.TypeEnPassant);
            }
        }
    }

    void KingMoves(int pos)
    {
        TryAdd(pos, pos - 9, 0);
        TryAdd(pos, pos - 8, 0);
        TryAdd(pos, pos - 7, 0);
        TryAdd(pos, pos - 1, 0);
        TryAdd(pos, pos + 1, 0);
        TryAdd(pos, pos + 1, 0);
        TryAdd(pos, pos + 7, 0);
        TryAdd(pos, pos + 8, 0);
        TryAdd(pos, pos + 9, 0);
        if (Piece.IsColor(IntBoard[pos], Piece.White))
        {
            if ((castlingRights & WKCastleMask) > 0 &&
                IntBoard[pos - 1] == 0 &&
                IntBoard[pos - 2] == 0 &&
                IntBoard[pos - 3] == 0) 
            {
                TryAdd(pos, pos - 2, Move.TypeCastle);
            }
            if ((castlingRights & WQCastleMask) > 0 && 
                IntBoard[pos + 1] == 0 && 
                IntBoard[pos + 2] == 0) 
            {
                TryAdd(pos, pos + 2, Move.TypeCastle);
            }
        } 
        else
        {
            if ((castlingRights & BKCastleMask) > 0 && 
                IntBoard[pos - 1] == 0 && 
                IntBoard[pos - 2] == 0 &&
                IntBoard[pos - 3] == 0) 
            {
                TryAdd(pos, pos - 2, Move.TypeCastle);
            }
            if ((castlingRights & BQCastleMask) > 0 && 
                IntBoard[pos + 1] == 0 && 
                IntBoard[pos + 2] == 0) 
            {
                TryAdd(pos, pos + 2, Move.TypeCastle);
            }
        }
    }

    void KnightMoves(int pos)
    {
        int x = pos & 0b111;
        int y = pos >> 3;
        if (x > 0)
        {
            if (y > 1) TryAdd(pos, pos - 17, Move.TypeNormal);
            if (y < 6) TryAdd(pos, pos + 15, Move.TypeNormal);
            if (x > 1) 
            { 
                if (y > 0) TryAdd(pos, pos - 10, Move.TypeNormal);
                if (y < 7) TryAdd(pos, pos + 6,  Move.TypeNormal);
            }
        }
        if (x < 7)
        {
            if (y > 1) TryAdd(pos, pos - 15, Move.TypeNormal);
            if (y < 6) TryAdd(pos, pos + 17, Move.TypeNormal);
            if (x < 6)
            {
                if (y > 0) TryAdd(pos, pos - 6,  0);
                if (y < 7) TryAdd(pos, pos + 10, 0);
            }
        }
    }

    static int FileInt(char c) => c switch {
            'a' => 0,
            'b' => 1,
            'c' => 2,
            'd' => 3,
            'e' => 4,
            'f' => 5,
            'g' => 6,
            'h' => 7,
            _ => -1 };
    
    static char FileChar(int n) => (char)(n + 'a');

    int FindPiece(int piece, int file, int rank) 
    {
        if (file == -1 && rank == -1) {
            for (int i = 0; i < 64; i++) {
                if (IntBoard[i] == piece) {
                    return i;
                }
            }
        } 
        else if (file == -1) {
            for (int i = 0; i < 8; i++) {
                if (IntBoard[rank*8+i] == piece) {
                    return rank*8+i;
                }
            }
        } 
        else if (rank == -1) {
            for (int i = file; i < 64; i += 8) {
                if (IntBoard[i] == piece) {
                    return i;
                }
            }
        } else {
            return rank*8+file;
        }
        return -1;
    }

    public Move MoveFromAN(string str, int color) 
    {

        /// CASTLING
        if (str[0] == 'O') { 
            int cStart = FindPiece(Piece.King | color, 4, -1);
            int cDest = (str.Length >= 5 && str[4] == 'O') ? cStart - 3 : cStart + 2;
            return new Move(cStart, cDest, Move.TypeCastle);
        }

        /// GAME OVER
        if ("10½+-".Contains(str[0])) return new Move(0, 0, 0);

        /// PARSE NORMAL MOVE STRING

        // Trim
        int i = 0;
        while (i < str.Length) {
            if ("x#=()/×:≠‡+!?□⌓tn⩲⩱±∓∞⯹".Contains(str[i]))
                str = str[..i] + str[(i + 1)..];
            else i++;
        }

        // Get Start Piece
        int startPiece = str[0] switch {
            'K' or '♔' or '♚' => Piece.King,
            'Q' or '♕' or '♛' => Piece.Queen,
            'R' or '♖' or '♜' => Piece.Rook,
            'B' or '♘' or '♞' => Piece.Bishop,
            'N' or '♗' or '♝' => Piece.Knight,
            _   => Piece.Pawn,
        };
        startPiece |= color;

        if (!Piece.IsType(startPiece, Piece.Pawn)) str = str[1..];
        
        // Find first coords set
        int start, dest;
        int file = FileInt(str[0]);
        if (file > -1) str = str[1..];
        char c = str[0];
        int rank = (c >= '1' && c <= '8') ? c-'1' : -1;
        if (file > -1) str = str[1..];
        // If no disambiguators present, current file/rank represent dest. Start must be found
        if (str.Length == 0) {
            start = FindPiece(startPiece, -1, -1);
        } 
        // If disambiguators present, use them to find start. Find second coords set for dest.
        else {
            start = FindPiece(startPiece, file, rank);
            file = FileInt(str[0]);
            c = str[1];
            rank = (c >= '1' && c <= '8') ? c-'1' : -1;
        }
        dest = rank*8+file-9;

        /// MOVE FLAGS
        int moveFlag = 0;

        // Capture Flags
        if (IntBoard[dest] > 0) {
            moveFlag = Piece.Type(IntBoard[dest]) & 0b1000;
        }

        // Pawn Flags
        if (Piece.IsType(startPiece, Piece.Pawn)) {
            if (str.Length == 3)
                moveFlag = str[2] switch {
                    'Q' => Move.TypePromoteToQueen,
                    'N' => Move.TypePromoteToKnight,
                    'R' => Move.TypePromoteToRook,
                    'B' => Move.TypePromoteToBishop,
                    _ => Move.TypeNormal
                };
            else if (Math.Abs(dest-start) == 16)
                moveFlag = Move.TypePawnLeap;
            else if (Math.Abs(dest-start) != 8 && IntBoard[dest] == 0)
                moveFlag = Move.TypeEnPassant;
        }

        // Woah, that took a while
        return new Move(start, dest, moveFlag);
    }

    public string ANFromMove(Move move) 
    {
        if (move.Value == 0) return "???";

        if (move.IsCastle) {
            if (Math.Abs(move.Dest - move.Start) == 2) {
                return "O-O";
            } else {
                return "O-O-O";
            }
        }

        StringBuilder str = new StringBuilder(7);

        // Add piece code
        if (!Piece.IsType(IntBoard[move.Start], Piece.Pawn)) {
            str.Append(Piece.Code(IntBoard[move.Start]));
        }

        // Find other identical pieces that can move to dest
        List<int> conflicts = new(6);
        for (int i = 0; i < 64; i++) {
            if ((i != move.Start) && (IntBoard[i] == IntBoard[move.Start])) {
                moveList = new(10);
                GenMoves(i);
                foreach (Move m in moveList) {
                    if (m.Dest == move.Dest) {
                        conflicts.Add(i);
                    }
                }
            }
        }

        // If other pieces can move to dest, add appropriate specifiers
        if (conflicts.Count > 0) {
            bool sameFile = false;
            bool sameRank = false;
            foreach (int i in conflicts) {
                if ((i & 0b111) == move.StartCol) sameFile = true;
                if ((i >> 3) == move.StartRow) sameRank = true;
            }
            if ((!sameFile) || (sameFile && sameRank))
                str.Append(FileChar(move.StartCol)); // File Specifier
            if (sameFile)
                str.Append(move.StartRow + 1); // Rank specifier
        }

        // Add capture marker if neccesary
        if (IntBoard[move.Dest] > 0 || move.IsEnPassant) {
            str.Append('x');
        }

        // Add destination
        str.Append(FileChar(move.DestCol));
        str.Append(move.DestRow + 1);

        // Add promotion marker if necessary
        if (move.IsPromotion) str.Append(move.Type switch {
            Move.TypePromoteToQueen  => "=Q",
            Move.TypePromoteToKnight => "=N",
            Move.TypePromoteToRook   => "=R",
            Move.TypePromoteToBishop => "=B",
            _ => "=?"
        });

        return str.ToString();
    }

    public int PieceAt(int index) 
    {
        return IntBoard[index];
    }

    public override string ToString()
    {
        StringBuilder str = new StringBuilder(74);
        for (int row = 0; row < 8; row++) {
            for (int col = 0; col < 8; col++) {
                str.Append(Piece.Code(IntBoard[row*8+col]));
                str.Append(' ');
            } str.Append('\n');
        }
        str.Append("Color To Move: " + Piece.ColorToString(ColorToMove));
        return str.ToString();
    }
}