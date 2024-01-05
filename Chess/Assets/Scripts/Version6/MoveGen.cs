using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Utils;
using Debug = UnityEngine.Debug;

namespace V6
{
    public sealed class MoveGen
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
        Stack<ulong> HashHistory;
        public int Fullmove;
        // Current state data:
        int halfmoveClock;
        public int pawnLeapFile; // 0 is A File, 7 is H file, 8 is no pawn leap
        int capturedPiece;
        public int castlingRights; // Format: KQkq
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
        public ulong[] Bitboards { get; set; }
        public ulong WhiteOccupyBoard { get; set; }
        public ulong WhiteAttackBoard { get; set; }
        public ulong BlackOccupyBoard { get; set; }
        public ulong BlackAttackBoard { get; set; }
        public ulong FullOccupyBoard { get; set; }
        // King indices:
        int kingIndex, wkIndex, bkIndex;
        # nullable enable
        Search? search = null;
        # nullable disable
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
        public ulong Hash { get; set; }
        // List of legal moves in current position
        PriorityQueue<Move, int> moveList; 
        public MoveGen(BoardStruct b) {
            IntBoard = b.IntBoard;
            moveList = new PriorityQueue<Move, int>(50);

            SetBitboards();

            WhiteToMove = b.WhiteToMove;
            if (WhiteToMove) {
                ColorToMove = Piece.White;
                OpponentColor = Piece.Black; 
            }
            else {
                ColorToMove = Piece.Black;
                OpponentColor = Piece.White;
            }

            for (int i = 0; i < 64; i++) {
                if (IntBoard[i] == Piece.WhiteKing) {
                    wkIndex = i;
                }
                if (IntBoard[i] == Piece.BlackKing) {
                    bkIndex = i;
                }
            }

            kingIndex = WhiteToMove ? wkIndex : bkIndex;

            castlingRights = b.CastlingRights;
            pawnLeapFile = b.PawnLeapFile;
            halfmoveClock = b.HalfmoveClock;
            capturedPiece = 0;

            StateData = (uint)(castlingRights | (pawnLeapFile << 9) | (halfmoveClock << 13));
            StateHistory = new Stack<uint>(70);
            StateHistory.Push(StateData);

            Hash = Zobrist.HashBoard(this);
            HashHistory = new(200);
            HashHistory.Push(Hash);
        }

        public MoveGen Clone() => new(ToStruct());

        public BoardStruct ToStruct() => new(IntBoard, WhiteToMove, castlingRights, pawnLeapFile, halfmoveClock);

        public void SetSearchObject(Search search) {
            this.search = search;
        }

        void SetBitboards() {
            Bitboards = new ulong[24];
            for (int i = 0; i < 64; i++) {
                if (IntBoard[i] > 0) {
                    Bitboards[IntBoard[i]] |= 1UL << i;
                }
            }
            WhiteOccupyBoard = WhiteAttackBoard = 0UL;
            BlackOccupyBoard = BlackAttackBoard = 0UL;
            for (int i = Piece.King; i <= Piece.Queen; i++) {
                WhiteOccupyBoard |= Bitboards[Piece.White | i];
                BlackOccupyBoard |= Bitboards[Piece.Black | i];
            }
        }
        public bool IsCheck() 
        {
            // Checks if king is currently in check
            PseudoLegalMoves();
            int kingIndex = (OpponentColor == Piece.White) ? wkIndex : bkIndex;
            while (moveList.Count > 0) {
                Move move = moveList.Dequeue();
                if (move.Dest == kingIndex)
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

            // Stop recursive search if king is captured
            if (search != null && Piece.IsType(IntBoard[dest], Piece.King)) {
                search.DeadKing = true;
            }

            // Unpack state
            castlingRights = (int)(StateData & 0b1111);
            Hash ^= Zobrist.pawnLeapFilesHash[(StateData >> 9) & 0b1111];
            pawnLeapFile = 8;
            halfmoveClock = (int)(((StateData >> 13) & 0b111111) + 1);
            capturedPiece = 0;

            // Handle pawn promotion moves
            if (move.IsPromotion)
            {
                halfmoveClock = 0;
                int promotion = movingColor | (type switch {
                    Move.TypePromoteToQueen => Piece.Queen,
                    Move.TypePromoteToKnight => Piece.Knight,
                    Move.TypePromoteToRook => Piece.Rook,
                    Move.TypePromoteToBishop => Piece.Bishop,
                    _ => 0
                });
                Promote(start, dest, promotion);
            }
            // Handles all other moves
            else switch (type)
            {
                // Castle moves
                case Move.TypeCastle:
                    FromToDoSafe(start, dest);
                    Hash ^= Zobrist.castleRightsHash[castlingRights];
                    switch (dest)
                    {
                        case 2:
                            FromToDoSafe(0, 3);
                            castlingRights &= 0b0011;
                            break;
                        case 6:
                            FromToDoSafe(7, 5);
                            castlingRights &= 0b0011;
                            break;
                        case 58:
                            FromToDoSafe(56, 59);
                            castlingRights &= 0b1100;
                            break;
                        case 62:
                            FromToDoSafe(63, 61);
                            castlingRights &= 0b1100;
                            break;
                    }
                    Hash ^= Zobrist.castleRightsHash[castlingRights];
                    break;
                // En passant captures
                case Move.TypeEnPassant:
                    if ((dest >> 3) == 0b010)
                        KillAt(dest + 8);
                    else
                        KillAt(dest - 8);
                    FromToDoSafe(start, dest);
                    break;
                // Pawn moving forward two spaces
                case Move.TypePawnLeap:
                    halfmoveClock = 0;
                    pawnLeapFile = start & 0b111; // Saves file state data so that it can be captured in future en passant
                    FromToDoSafe(start, dest);
                    break;
                // All other moves
                default:
                    if (movingPiece == Piece.King)
                    {
                        // King moves remove castling rights
                        Hash ^= Zobrist.castleRightsHash[castlingRights];
                        if (movingColor == Piece.White) 
                            castlingRights &= 0b0011;
                        else 
                            castlingRights &= 0b1100;
                        Hash ^= Zobrist.castleRightsHash[castlingRights];
                    }
                    else if (movingPiece == Piece.Rook)
                    {
                        // Rook moves remove castling rights on their side
                        Hash ^= Zobrist.castleRightsHash[castlingRights];
                        switch (start)
                        {
                            case 0:
                                castlingRights &= WQCastleMask ^ 15;
                                break;
                            case 7:
                                castlingRights &= WKCastleMask ^ 15;
                                break;
                            case 56:
                                castlingRights &= BQCastleMask ^ 15;
                                break;
                            case 63:
                                castlingRights &= BKCastleMask ^ 15;
                                break;
                        }
                        Hash ^= Zobrist.castleRightsHash[castlingRights];
                    }
                    else if (movingPiece == Piece.Pawn) {
                        halfmoveClock = 0; // Reset halfmove clock if pawn is moved
                    }
                    FromToDo(start, dest);
                    break;
            }

            if (capturedPiece != 0) {
                halfmoveClock = 0; // Reset halfmove clock if piece is captured 
            } 

            Hash ^= Zobrist.pawnLeapFilesHash[pawnLeapFile & 0b1111];

            // Repack state data
            // Format: MMMMMMMMMMMMMMHHHHHHLLLLPPPPPCCCC, 
            // M = Move, H = halfmoveClock, L = pawnLeapFile, P = capturedPiece, C = castlingRights
            StateData = (uint)castlingRights | ((uint)capturedPiece << 4) | ((uint)pawnLeapFile << 9) | ((uint)halfmoveClock << 13);
            // Save state data
            StateHistory.Push(StateData);
            HashHistory.Push(Hash);

            SwapTurn();
        }

        public void UndoMove(Move move)
        {
            // Unpack state data
            StateData = StateHistory.Pop();
            Hash = HashHistory.Pop();

            castlingRights = (int)StateData & 0b1111;
            capturedPiece = (int)(StateData >> 4) & 0b11111;
            pawnLeapFile = (int)(StateData >> 9) & 0b1111;
            halfmoveClock = (int)(StateData >> 13) & 0b111111;

            // Unpack move
            int start = move.Start;
            int dest = move.Dest;
            int type = move.Type;
            int movingPiece = Piece.Type(IntBoard[dest]);
            int movingColor = Piece.Color(IntBoard[dest]);

            // Handles king index
            if (movingPiece == Piece.King) {
                if (movingColor == Piece.White)
                    wkIndex = start;
                else
                    bkIndex = start;
            }

            int whiteRooks = 0;
            int blackRooks = 0;

            for (int i = 0; i < 64; i++) {
                if (IntBoard[i] == Piece.WhiteRook) whiteRooks++;
                else if (IntBoard[i] == Piece.BlackRook) blackRooks++;
            }

            bool normal = whiteRooks < 3 && blackRooks < 3;


            if (move.IsPromotion)
                // Undo pawn promotion
                Depromote(dest, start, capturedPiece);
            else switch (type) {
                // Undo En Passant
                case Move.TypeEnPassant:
                    FromToUndoSafe(dest, start);
                    if ((dest >> 3) == 0b010)
                        ReviveAt(dest + 8, Piece.WhitePawn);
                    else
                        ReviveAt(dest - 8, Piece.BlackPawn);
                    break;
                // Undo Castle
                case Move.TypeCastle:
                    FromToUndoSafe(dest, start);
                    switch (dest) {
                        case 2:
                            FromToUndoSafe(3, 0);
                            break;
                        case 6:
                            FromToUndoSafe(5, 7);
                            break;
                        case 58:
                            FromToUndoSafe(59, 56);
                            break;
                        case 62:
                            FromToUndoSafe(63, 61);
                            break;
                    }
                    break;
                // Undo all other moves
                default:
                    FromToUndo(dest, start, capturedPiece);
                    break;
            }

            whiteRooks = 0;
            blackRooks = 0;

            for (int i = 0; i < 64; i++) {
                if (IntBoard[i] == Piece.WhiteRook) whiteRooks++;
                else if (IntBoard[i] == Piece.BlackRook) blackRooks++;
            }

            if (normal && (blackRooks > 2 || whiteRooks > 2)) {
                Debug.Log("Rook duplicated with move " + move.ToString +
                        "\nWR = " + whiteRooks + ", BR = " + blackRooks + 
                        "\nNew board:\n" + ToStruct().ToString() +
                        "\nIsCastle = " + (Piece.IsType(IntBoard[dest], Piece.King) && Math.Abs(start - dest) == 2) +
                        "\nMoving Piece = " + Piece.ToString(movingPiece | movingColor) +
                        "\nStart = " + start + "\nDest = " + dest + 
                        "\nCaptured Piece = " + Piece.ToString(capturedPiece));
            }

            SwapTurn();

            StateData = StateHistory.Peek();

            castlingRights = (int)StateData & 0b1111;
            capturedPiece = (int)(StateData >> 4) & 0b11111;
            pawnLeapFile = (int)(StateData >> 9) & 0b1111;
            halfmoveClock = (int)(StateData >> 13) & 0b111111;
        }

        void FromToDo(int from, int to)
        {
            // Moves a piece from one square to another
            // Handles IntBoard, bitboard, and hash representations
            // Checks for captures
            if (IntBoard[to] > 0) {
                capturedPiece = IntBoard[to];
                Bitboards[capturedPiece] ^= 1UL << to;
                Hash ^= Zobrist.pieceHash[capturedPiece, to];
            }
            Bitboards[IntBoard[from]] ^= (1UL << from) | (1UL << to);
            Hash ^= Zobrist.pieceHash[IntBoard[from], from] ^ Zobrist.pieceHash[IntBoard[from], to];
            IntBoard[to] = IntBoard[from];
            IntBoard[from] = 0;
        }

        void FromToUndo(int from, int to, int capture) {
            // Same as before, but takes a previously captured piece as input
            // And revives it if applicable
            // Does not update hash
            if (capture > 0) {
                Bitboards[capture] |= 1UL << from;
            }
            Bitboards[IntBoard[from]] ^= (1UL << from) | (1UL << to);
            IntBoard[to] = IntBoard[from];
            IntBoard[from] = capture;
        }

        void FromToDoSafe(int from, int to) 
        {
            // Assumes no captures involved
            Bitboards[IntBoard[from]] ^= (1UL << from) | (1UL << to);
            Hash ^= Zobrist.pieceHash[IntBoard[from], from] ^ Zobrist.pieceHash[IntBoard[from], to];
            IntBoard[to] = IntBoard[from];
            IntBoard[from] = 0;
        }

        void FromToUndoSafe(int from, int to) {
            // Same as before, but does not update hash
            Bitboards[IntBoard[from]] ^= (1UL << from) | (1UL << to);
            IntBoard[to] = IntBoard[from];
            IntBoard[from] = 0;
        }

        void KillAt(int index) 
        {
            // Removes a piece from the board, handling intboard, bitboards, and hash
            capturedPiece = IntBoard[index];
            Bitboards[capturedPiece] ^= 1UL << index;
            Hash ^= Zobrist.pieceHash[capturedPiece, index];
            IntBoard[index] = 0;
        }

        void ReviveAt(int index, int piece) 
        {
            // Creates a piece on the board, used for move undoing, handles intboard and bitboards
            Bitboards[piece] |= 1UL << index;
            IntBoard[index] = piece;
        }

        void Promote(int from, int to, int promotion) 
        {
            // Promotes pawn, handles intboard, bitboard, and hash
            if (IntBoard[to] > 0) {
                capturedPiece = IntBoard[to];
                Bitboards[capturedPiece] ^= 1UL << to;
                Hash ^= Zobrist.pieceHash[capturedPiece, to];
            }
            Bitboards[IntBoard[from]] ^= 1UL << from;
            Hash ^= Zobrist.pieceHash[IntBoard[from], from];
            Bitboards[promotion] |= 1UL << to;
            Hash ^= Zobrist.pieceHash[promotion, to];
            IntBoard[from] = 0;
            IntBoard[to] = promotion;
        }

        void Depromote(int from, int to, int capture) 
        {
            // Undoes pawn promotion, handles intboard and bitboards
            int pawn = Piece.Pawn | (Piece.Color(IntBoard[to]));
            if (capture > 0) {
                Bitboards[capture] |= 1UL << from;
            }
            Bitboards[IntBoard[from]] ^= 1UL << from;
            Bitboards[pawn] |= 1UL << to;
            IntBoard[from] = capture;
            IntBoard[to] = pawn;
        }

        void SwapTurn() 
        {
            WhiteToMove = !WhiteToMove;
            Hash ^= Zobrist.blackToMoveHash;
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

        void GenerateOccupyBoards() {
            // Needlessly aesthetic way to generate occupancy bitboards
            WhiteOccupyBoard = Bitboards[Piece.WhiteKing]
                             | Bitboards[Piece.WhiteQueen]
                             | Bitboards[Piece.WhiteRook]
                             | Bitboards[Piece.WhiteKnight]
                             | Bitboards[Piece.WhiteBishop]
                             | Bitboards[Piece.WhitePawn];
            BlackOccupyBoard = Bitboards[Piece.BlackKing]
                             | Bitboards[Piece.BlackQueen]
                             | Bitboards[Piece.BlackRook]
                             | Bitboards[Piece.BlackKnight]
                             | Bitboards[Piece.BlackBishop]
                             | Bitboards[Piece.BlackPawn];
            FullOccupyBoard  = WhiteOccupyBoard 
                             | BlackAttackBoard;
        }

        public PriorityQueue<Move, int> PseudoLegalMoves()
        {
            moveList = new(50);

            GenerateOccupyBoards();

            for (int pos = 0; pos < 64; pos++)
                if (Piece.IsColor(IntBoard[pos], ColorToMove))
                    GenMoves(pos);

            return moveList;
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

        public PriorityQueue<Move, int> CullIllegalMoves(PriorityQueue<Move, int> moveList) 
        {
            PriorityQueue<Move, int> legalMoves = new(moveList.Count);
            List<Move> moves = new(moveList.Count);
            while (moveList.Count > 0)
                moves.Add(moveList.Dequeue());
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
                    legalMoves.Enqueue(m, m.MinScore);
                UndoMove(m);
            }
            return legalMoves;
        }
        
        public PriorityQueue<Move, int> LegalMoves() => CullIllegalMoves(PseudoLegalMoves());

        public ulong DrawKillMap(PriorityQueue<Move, int> moves) {
            ulong killMap = 0UL;
            while (moves.Count > 0) {
                killMap |= 1UL << moves.Dequeue().Dest;
            }
            return killMap;
        }    

        void TryAdd(int pos1, int pos2, int flag)
        {
            if (pos1 < 0 || pos1 > 63 || pos2 < 0 || pos2 > 63) return;
            if (Piece.IsColor(IntBoard[pos2], ColorToMove)) return;
            if (flag == -1 && (pos2 >> 3) % 7 == 0) {
                Move move1 = new(pos1, pos2, Move.TypePromoteToQueen);
                Move move2 = new(pos1, pos2, Move.TypePromoteToKnight);
                moveList.Enqueue(move1, move1.MinScore);
                moveList.Enqueue(move2, move2.MinScore);
            }
            else {
                if (flag == -1) flag++;
                if (Piece.IsColor(IntBoard[pos2], OpponentColor) && flag == Move.TypeNormal)
                    flag = Piece.Type(IntBoard[pos2]) + 8;
                Move m = new(pos1, pos2, flag);
                moveList.Enqueue(m, m.MinScore);
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
            int posFile = pos & 0b111;
            int posRank = pos >> 3;
            if (WhiteToMove)
            {
                if (IntBoard[pos + 8] == 0) { 
                    TryAdd(pos, pos + 8, -1); // Push up one
                    if (posRank == 1 && IntBoard[pos + 16] == 0)
                        TryAdd(pos, pos + 16, Move.TypePawnLeap); // Push up two
                }
                if (posFile > 0 && Piece.IsColor(IntBoard[pos + 7], OpponentColor)) {
                    TryAdd(pos, pos + 7, -1); // Pawn attack up-left
                } 
                else if (posFile - 1 == pawnLeapFile && posRank == 4) {
                    TryAdd(pos, pos + 7, Move.TypeEnPassant); // Pawn en passant capture left
                }
                if (posFile < 7 && Piece.IsColor(IntBoard[pos + 9], OpponentColor)) { 
                    TryAdd(pos, pos + 9, -1); // Pawn attack up-right
                } 
                else if (pawnLeapFile < 8 && posFile + 1 == pawnLeapFile && posRank == 4) {
                    TryAdd(pos, pos + 9, Move.TypeEnPassant); // Pawn en passant capture right
                }
            }
            else
            {
                if (IntBoard[pos - 8] == 0)
                {
                    TryAdd(pos, pos - 8, -1);
                    if (posRank == 6 && IntBoard[pos - 16] == 0)
                        TryAdd(pos, pos - 16, 3);
                }
                if (posFile < 7 && Piece.IsColor(IntBoard[pos - 7], OpponentColor)) {
                    TryAdd(pos, pos - 7, -1);
                } 
                else if (pawnLeapFile < 8 && posFile + 1 == pawnLeapFile && posRank == 3) {
                    TryAdd(pos, pos - 7, Move.TypeEnPassant);
                }
                if (posFile > 0 && Piece.IsColor(IntBoard[pos - 9], OpponentColor)) {
                    TryAdd(pos, pos - 9, -1);
                } 
                else if (posFile - 1 == pawnLeapFile && posRank == 3) {
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
                if ((castlingRights & WQCastleMask) > 0 &&
                    IntBoard[pos - 1] == 0 &&
                    IntBoard[pos - 2] == 0 &&
                    IntBoard[pos - 3] == 0) 
                {
                    TryAdd(pos, pos - 2, Move.TypeCastle);
                }
                if ((castlingRights & WKCastleMask) > 0 && 
                    IntBoard[pos + 1] == 0 && 
                    IntBoard[pos + 2] == 0) 
                {
                    TryAdd(pos, pos + 2, Move.TypeCastle);
                }
            } 
            else
            {
                if ((castlingRights & BQCastleMask) > 0 && 
                    IntBoard[pos - 1] == 0 && 
                    IntBoard[pos - 2] == 0 &&
                    IntBoard[pos - 3] == 0) 
                {
                    TryAdd(pos, pos - 2, Move.TypeCastle);
                }
                if ((castlingRights & BKCastleMask) > 0 && 
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
    }
}