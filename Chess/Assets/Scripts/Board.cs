using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;


sealed class Board
{
    public static readonly char[] LetterCodes = new char[] {'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'};
    public int[] board { get; }
    public bool WhiteToMove { get; set; }
    public int ColorToMove { get; set; }
    public int OpponentColor { get; set; }
    public int StateData; // Format: HHHHHHLLLLPPPPPCCCC, H = halfmoveClock, L = pawnLeapFile, P = capturedPiece, C = castlingRights
    public Stack<int> StateHistory { get; }

    int halfmoveClock;
    int pawnLeapFile; // 0 is A File, 7 is H file, 8 is no pawn leap
    int capturedPiece;
    int castlingRights; // Format: KQkq
    const int WKCastleMask = 0b1000; // White Kingside Castle Mask
    const int WQCastleMask = 0b0100; // White Queenside Castle Mask
    const int BKCastleMask = 0b0010; // Black Kingside Castle Mask
    const int BQCastleMask = 0b0001; // Black Queenside Castle Mask
    int kingIndex, wkIndex, bkIndex;
    public enum Status{
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


    List<Move> moveList; // List of legal moves in a given position

    public Board()
    {
        board = StartingBoard();
        moveList = new List<Move>(50);
        WhiteToMove = true;
        ColorToMove = Piece.White;
        OpponentColor = Piece.Black;
        castlingRights = 0b1111;
        pawnLeapFile = 8;
        halfmoveClock = 0;
        capturedPiece = 0;
        StateData = castlingRights | (capturedPiece << 4) | (pawnLeapFile << 9) | (halfmoveClock << 13);
        StateHistory = new Stack<int>(70);
    }

    public Board(int[] boardArray) : this() {
        board = boardArray;
    }
    
    public static int[] StartingBoard() {
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

    public static Board BoardFromIntArray(int[] boardArray) {
        return new Board(boardArray);
    }

    public Status GameStatus() {
        if (halfmoveClock >= 50)
            return Status.DrawBy50MoveRule;
        PseudoLegalMoves();
        Move[] moves = new Move[moveList.Count];
        moveList.CopyTo(moves);
        bool isThreatened = IsCheck();
        foreach(Move move in moves) {
            DoMove(move);
            if (IsCheck()) {
                if (isThreatened)
                    return Status.WinByResignation;
                else
                    return Status.DrawByStalemate;
            }
            UndoMove(move);
        }
        return Status.Running;
    }
    public bool IsCheck() {
        PseudoLegalMoves();
        foreach(Move move in moveList) {
            if (move.Dest == (OpponentColor == Piece.White ? wkIndex : bkIndex))
                return true;
        }
        return false;
    }

    public void DoMove(Move move)
    {
        int start = move.Start;
        int dest = move.Dest;
        int type = move.Type;

        int movingPiece = Piece.Type(board[start]);
        int movingColor = Piece.Color(board[start]);

        if (movingPiece == Piece.King) {
            if (movingColor == Piece.White) wkIndex = dest;
            else bkIndex = dest;
        }

        if (movingColor == Piece.White) kingIndex = wkIndex;
        else kingIndex = bkIndex;


        castlingRights = StateData & 0b1111;
        pawnLeapFile = 8;
        halfmoveClock = (StateData >> 13) + 1;

        if (move.IsPromotion)
        {
            halfmoveClock = 0;
            board[start] = 0;
            capturedPiece = board[dest];
            switch (type)
            {
                case Move.TypePromoteToQueen:
                    board[dest] = Piece.Queen | movingColor;
                    break;
                case Move.TypePromoteToKnight:
                    board[dest] = Piece.Knight | movingColor;
                    break;
                case Move.TypePromoteToBishop:
                    board[dest] = Piece.Bishop | movingColor;
                    break;
                case Move.TypePromoteToRook:
                    board[dest] = Piece.Rook | movingColor;
                    break;
            }
        }
        else switch (type)
        {
            case Move.TypeCastle:
                capturedPiece = 0;
                board[dest] = board[start];
                board[start] = 0;
                switch (dest)
                {
                    case 1:
                        board[0] = 0;
                        board[2] = Piece.WhiteRook;
                        castlingRights &= 0b0011;
                        break;
                    case 5:
                        board[7] = 0;
                        board[4] = Piece.WhiteRook;
                        castlingRights &= 0b0011;
                        break;
                    case 57:
                        board[56] = 0;
                        board[58] = Piece.BlackRook;
                        castlingRights &= 0b1100;
                        break;
                    case 61:
                        board[63] = 0;
                        board[60] = Piece.BlackRook;
                        castlingRights &= 0b1100;
                        break;
                }
                break;
            case Move.TypeEnPassant:
                if ((dest >> 3) == 0b010)
                {
                    capturedPiece = Piece.WhitePawn;
                    board[dest + 8] = 0;
                }
                else
                {
                    capturedPiece = Piece.BlackPawn;
                    board[dest - 8] = 0;
                }
                board[dest] = board[start];
                board[start] = 0;
                break;
            case Move.TypePawnLeap:
                halfmoveClock = 0;
                pawnLeapFile = start >> 3;
                capturedPiece = 0;
                board[dest] = board[start];
                board[start] = 0;
                break;
            default:
                if (movingPiece == Piece.King)
                {
                    if (movingColor == Piece.White) castlingRights &= 0b0011;
                    else castlingRights &= 0b1100;
                }
                else if (movingPiece == Piece.Rook)
                {
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
                else if (movingPiece == Piece.Pawn) halfmoveClock = 0;
                capturedPiece = board[dest];
                board[dest] = board[start];
                board[start] = 0;
                break;
        }

        if (capturedPiece != 0) halfmoveClock = 0;

        // Format: HHHHHHLLLLPPPPPCCCC, H = halfmoveClock, L = pawnLeapFile, P = capturedPiece, C = castlingRights

        StateData = castlingRights | (capturedPiece << 4) | (pawnLeapFile << 9) | (halfmoveClock << 13);
        StateHistory.Push(StateData);
        WhiteToMove = !WhiteToMove;
        if (WhiteToMove)
        {
            ColorToMove = Piece.White;
            OpponentColor = Piece.Black;
        }
        else
        {
            ColorToMove = Piece.Black;
            OpponentColor = Piece.White;
        }
    }

    public void UndoMove(Move move)
    {
        int start = move.Start;
        int dest = move.Dest;
        int type = move.Type;

        StateData = StateHistory.Pop();

        castlingRights = StateData & 0b1111;
        capturedPiece = (StateData >> 4) & 0b11111;
        pawnLeapFile = (StateData >> 9) & 0b1111;
        halfmoveClock = StateData >> 13;

        if (move.IsPromotion)
        {
            board[start] = Piece.Pawn | Piece.Color(board[dest]);
            board[dest] = capturedPiece;
        }
        else switch (type)
            {
                case Move.TypeCastle:
                    capturedPiece = 0;
                    board[start] = board[dest];
                    board[dest] = 0;
                    switch (dest)
                    {
                        case 1:
                            board[0] = Piece.WhiteRook;
                            board[2] = 0;
                            break;
                        case 5:
                            board[7] = Piece.WhiteRook;
                            board[4] = 0;
                            break;
                        case 57:
                            board[56] = Piece.BlackRook;
                            board[58] = 0;
                            break;
                        case 61:
                            board[63] = Piece.BlackRook;
                            board[60] = 0;
                            break;
                    }
                    break;
                case Move.TypeEnPassant:
                    if ((dest >> 3) == 0b010) 
                        board[dest + 8] = Piece.WhitePawn;
                    else 
                        board[dest - 8] = Piece.BlackPawn;
                    board[start] = board[dest];
                    board[dest] = 0;
                    break;
                default:
                    board[start] = board[dest];
                    board[dest] = capturedPiece;
                    break;
            }

        WhiteToMove = !WhiteToMove;
        if (WhiteToMove)
        {
            ColorToMove = Piece.White;
            OpponentColor = Piece.Black;
        }
        else
        {
            ColorToMove = Piece.Black;
            OpponentColor = Piece.White;
        }
    }

    public List<Move> PseudoLegalMoves()
    {
        moveList = new List<Move>(50);

        for (int pos = 0; pos < 64; pos++)
            if (Piece.IsColor(board[pos], ColorToMove))
                GenMoves(pos);

        return moveList;
    }

    public void GenMoves(int pos) {
        if (Piece.CanSlide(board[pos]))
                    SlidingMoves(pos);
        else switch (Piece.Type(board[pos]))
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

    int FindPiece(int piece, int file, int rank) {
        if (file == -1 && rank == -1) {
            for (int i = 0; i < 64; i++) {
                if (board[i] == piece) {
                    return i;
                }
            }
        } 
        else if (file == -1) {
            for (int i = 0; i < 8; i++) {
                if (board[rank*8+i] == piece) {
                    return rank*8+i;
                }
            }
        } 
        else if (rank == -1) {
            for (int i = file; i < 64; i += 8) {
                if (board[i] == piece) {
                    return i;
                }
            }
        } else {
            return rank*8+file;
        }
        return -1;
    }

    public Move MoveFromAN(string str, int color) {

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
        if (board[dest] > 0) {
            moveFlag = Piece.Type(board[dest]) & 0b1000;
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
            else if (Math.Abs(dest-start) != 8 && board[dest] == 0)
                moveFlag = Move.TypeEnPassant;
        }

        // Woah, that took a while
        return new Move(start, dest, moveFlag);
    }

    public string ANFromMove(Move move) {
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
        if (!Piece.IsType(board[move.Start], Piece.Pawn)) {
            str.Append(Piece.Code(board[move.Start]));
        }

        // Find other identical pieces that can move to dest
        List<int> conflicts = new(6);
        for (int i = 0; i < 64; i++) {
            if ((i != move.Start) && (board[i] == board[move.Start])) {
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
        if (board[move.Dest] > 0 || move.IsEnPassant) {
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

    void TryAdd(int pos1, int pos2, int flag)
    {
        if (pos1 < 0 || pos1 > 63 || pos2 < 0 || pos2 > 63) return;
        if (Piece.IsColor(board[pos2], ColorToMove)) return;
        if (flag == -1 && (pos2 >> 3) % 7 == 0) {
            moveList.Add(new Move(pos1, pos2, Move.TypePromoteToKnight));
            moveList.Add(new Move(pos1, pos2, Move.TypePromoteToQueen));
        }
        else {
            if (flag == -1) flag++;
            if (Piece.IsColor(board[pos2], OpponentColor) && flag == Move.TypeNormal)
                flag = Piece.Type(board[pos2]) + 8;
            moveList.Add(new Move(pos1, pos2, flag));
        }
    }

    void SlidingMoves(int pos)
    {
        if (Piece.CanSlideStraight(board[pos]))
        {
            SlidePiece(pos, 1, 0);
            SlidePiece(pos, -1, 0);
            SlidePiece(pos, 0, 1);
            SlidePiece(pos, 0, -1);
        }
        if (Piece.CanSlideDiagonal(board[pos]))
        {
            SlidePiece(pos, 1, 1);
            SlidePiece(pos, 1, -1);
            SlidePiece(pos, -1, 1);
            SlidePiece(pos, -1, -1);
        }
    }

    void SlidePiece(int pos1, int offsetX, int offsetY)
    {
        int color = Piece.Color(board[pos1]);
        int range = Math.Min(
            ((pos1 & 0b111) * -offsetX + 7) % 7 + ((offsetX ^ 0b1) << 3),
            ((pos1 >> 3) * -offsetY + 7) % 7 + ((offsetY ^ 0b1) << 3)
            );
        int offset = offsetX + (offsetY * 8);
        int pos2 = pos1;
        for (int i = 0; i < range; i++)
        {
            pos2 += offset;
            if (board[pos2] == 0) TryAdd(pos1, pos2, 0);
            else {
                if (!Piece.IsColor(board[pos2], color)) TryAdd(pos1, pos2, 0);
                break;
            }
        }
    }

    void PawnMoves(int pos)
    {
        if (WhiteToMove)
        {
            if (board[pos + 8] == 0) { 
                TryAdd(pos, pos + 8, -1);
                if (pos >> 3 == 1 && board[pos + 16] == 0)
                    TryAdd(pos, pos + 16, Move.TypePawnLeap);
            }
            if (Piece.IsColor(board[pos + 7], OpponentColor)) {
                TryAdd(pos, pos + 7, -1);
            } else if ((pos%8)-1 == pawnLeapFile && pos/8 == 4) {
                TryAdd(pos, pos + 7, Move.TypeEnPassant);
            }
            if (Piece.IsColor(board[pos + 9], OpponentColor)) { 
                TryAdd(pos, pos + 9, -1);
            } else if ((pos%8)+1 == pawnLeapFile && pos/8 == 4) {
                TryAdd(pos, pos + 9, Move.TypeEnPassant);
            }
        }
        else
        {
            if (board[pos - 8] == 0)
            {
                TryAdd(pos, pos - 8, -1);
                if (pos >> 3 == 6 && board[pos - 16] == 0)
                    TryAdd(pos, pos - 16, 3);
            }
            if (Piece.IsColor(board[pos - 7], OpponentColor)) {
                TryAdd(pos, pos - 7, -1);
            } else if ((pos%8)+1 == pawnLeapFile && pos/8 == 3) {
                TryAdd(pos, pos - 7, Move.TypeEnPassant);
            }
            if (Piece.IsColor(board[pos - 9], OpponentColor)) {
                TryAdd(pos, pos - 9, -1);
            } else if ((pos%8)-1 == pawnLeapFile && pos/8 == 3) {
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
        if (Piece.IsColor(board[pos], Piece.White))
        {
            if ((castlingRights & WKCastleMask) > 0 &&
                board[pos - 1] == 0 &&
                board[pos - 2] == 0) 
            {
                TryAdd(pos, pos - 2, Move.TypeCastle);
            }
            if ((castlingRights & WQCastleMask) > 0 && 
                board[pos + 1] == 0 && 
                board[pos + 2] == 0 && 
                board[pos + 3] == 0) 
            {
                TryAdd(pos, pos + 2, Move.TypeCastle);
            }
        } 
        else
        {
            if ((castlingRights & BKCastleMask) > 0 && 
                board[pos - 1] == 0 && 
                board[pos - 2] == 0) 
            {
                TryAdd(pos, pos - 2, Move.TypeCastle);
            }
            if ((castlingRights & BQCastleMask) > 0 && 
                board[pos + 1] == 0 && 
                board[pos + 2] == 0 && 
                board[pos + 3] == 0) 
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

    public override string ToString()
    {
        StringBuilder str = new StringBuilder(74);
        for (int row = 0; row < 8; row++) {
            for (int col = 0; col < 8; col++) {
                str.Append(Piece.Code(board[row*8+col]));
                str.Append(' ');
            } str.Append('\n');
        }
        str.Append("Color To Move: " + Piece.ColorToString(ColorToMove));
        return str.ToString();
    }
}