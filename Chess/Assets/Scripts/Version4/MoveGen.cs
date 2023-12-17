using System;
using System.Collections.Generic;
using Utils;

namespace V4
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
        public long Hash { get; set; }
        // List of legal moves in current position
        PriorityQueue<Move, int> moveList; 
        public MoveGen(BoardStruct b) {
            IntBoard = b.IntBoard;
            moveList = new PriorityQueue<Move, int>(50);

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
                if (IntBoard[i] == (Piece.White | Piece.King)) {
                    wkIndex = i;
                }
                if (IntBoard[i] == (Piece.Black | Piece.King)) {
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
        }

        MoveGen Clone() => new(ToStruct());

        public BoardStruct ToStruct() => new(IntBoard, WhiteToMove, castlingRights, pawnLeapFile, halfmoveClock);

        public void SetSearchObject(Search search) {
            this.search = search;
        }
        public bool IsCheck() 
        {
            // Checks if king is currently in check
            PseudoLegalMoves();
            while (moveList.Count > 0) {
                Move move = moveList.Dequeue();
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

            // Stop recursive search if king is captured
            if (search != null && Piece.IsType(IntBoard[dest], Piece.King)) {
                search.DeadKing = true;
            }

            // Unpack state
            castlingRights = (int)(StateData & 0b1111);
            Hash ^= Zobrist.pawnLeapFilesHash[(StateData >> 9) & 0b1111];
            pawnLeapFile = 8;
            halfmoveClock = (int)(((StateData >> 13) & 0b111111) + 1);

            if (WhiteToMove) {
                Hash ^= Zobrist.whitePieceHash[movingPiece, start];
                if (!move.IsPromotion) {
                    Hash ^= Zobrist.whitePieceHash[movingPiece, dest];
                }
                if (IntBoard[dest] > 0) {
                    Hash ^= Zobrist.blackPieceHash[movingPiece, dest];
                }
            }
            else {
                Hash ^= Zobrist.blackPieceHash[movingPiece, start];
                if (!move.IsPromotion) {
                    Hash ^= Zobrist.blackPieceHash[movingPiece, dest];
                }
                if (IntBoard[dest] > 0) {
                    Hash ^= Zobrist.whitePieceHash[movingPiece, dest];
                }
            }

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
                if (WhiteToMove)
                    Hash ^= Zobrist.whitePieceHash[IntBoard[dest], dest];
                else
                    Hash ^= Zobrist.whitePieceHash[IntBoard[dest], dest];
            }
            // Handles all other moves
            else switch (type)
            {
                // Castle moves
                case Move.TypeCastle:
                    capturedPiece = 0;
                    IntBoard[dest] = IntBoard[start];
                    IntBoard[start] = 0;
                    Hash ^= Zobrist.castleRightsHash[castlingRights];
                    switch (dest)
                    {
                        case 2:
                            IntBoard[0] = 0;
                            IntBoard[3] = Piece.WhiteRook;
                            Hash ^= Zobrist.whitePieceHash[Piece.Rook, 0] ^ Zobrist.whitePieceHash[Piece.Rook, 3];
                            castlingRights &= 0b0011;
                            break;
                        case 6:
                            IntBoard[7] = 0;
                            IntBoard[5] = Piece.WhiteRook;
                            Hash ^= Zobrist.whitePieceHash[Piece.Rook, 7] ^ Zobrist.whitePieceHash[Piece.Rook, 5];
                            castlingRights &= 0b0011;
                            break;
                        case 58:
                            IntBoard[56] = 0;
                            IntBoard[59] = Piece.BlackRook;
                            Hash ^= Zobrist.blackPieceHash[Piece.Rook, 56] ^ Zobrist.blackPieceHash[Piece.Rook, 59];
                            castlingRights &= 0b1100;
                            break;
                        case 62:
                            IntBoard[63] = 0;
                            IntBoard[61] = Piece.BlackRook;
                            Hash ^= Zobrist.blackPieceHash[Piece.Rook, 63] ^ Zobrist.blackPieceHash[Piece.Rook, 61];
                            castlingRights &= 0b1100;
                            break;
                    }
                    Hash ^= Zobrist.castleRightsHash[castlingRights];
                    break;
                // En passant captures
                case Move.TypeEnPassant:
                    if ((dest >> 3) == 0b010)
                    {
                        capturedPiece = Piece.WhitePawn;
                        IntBoard[dest + 8] = 0;
                        Hash ^= Zobrist.whitePieceHash[Piece.WhitePawn, dest + 8];
                    }
                    else
                    {
                        capturedPiece = Piece.BlackPawn;
                        IntBoard[dest - 8] = 0;
                        Hash ^= Zobrist.blackPieceHash[Piece.BlackPawn, dest - 8];
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
                        Hash ^= Zobrist.castleRightsHash[castlingRights];
                        if (movingColor == Piece.White) castlingRights &= 0b0011;
                        else castlingRights &= 0b1100;
                        Hash ^= Zobrist.castleRightsHash[castlingRights];
                    }
                    else if (movingPiece == Piece.Rook)
                    {
                        // Rook moves remove castling rights on their side
                        Hash ^= Zobrist.castleRightsHash[castlingRights];
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
                        Hash ^= Zobrist.castleRightsHash[castlingRights];
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

            Hash ^= Zobrist.pawnLeapFilesHash[pawnLeapFile & 0b1111];

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

            Hash ^= Zobrist.castleRightsHash[castlingRights] ^ Zobrist.pawnLeapFilesHash[pawnLeapFile];

            castlingRights = (int)StateData & 0b1111;
            capturedPiece = (int)(StateData >> 4) & 0b11111;
            pawnLeapFile = (int)(StateData >> 9) & 0b1111;
            halfmoveClock = (int)(StateData >> 13) & 0b111111;
            int move = (int)(StateData >> 19);

            Hash ^= Zobrist.castleRightsHash[castlingRights] ^ Zobrist.pawnLeapFilesHash[pawnLeapFile];

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
                if ((dest >> 3) == 0) {
                    // Undoes black promotion
                    Hash ^= Zobrist.blackPieceHash[movingPiece, dest];
                    Hash ^= Zobrist.blackPieceHash[Piece.Pawn, start];
                    IntBoard[start] = Piece.BlackPawn;
                    IntBoard[dest] = capturedPiece;
                    if (capturedPiece > 0) {
                        Hash ^= Zobrist.whitePieceHash[capturedPiece & 0b111, dest];
                    }
                }
                else if ((dest >> 3) == 7) {
                    // Undoes white promotion
                    Hash ^= Zobrist.whitePieceHash[movingPiece, dest];
                    Hash ^= Zobrist.whitePieceHash[Piece.Pawn, start];
                    IntBoard[start] = Piece.WhitePawn;
                    IntBoard[dest] = capturedPiece;
                    if (capturedPiece > 0) {
                        Hash ^= Zobrist.blackPieceHash[capturedPiece & 0b111, dest];
                    }
                }
                else {
                    // Undoes En Passant
                    if ((dest >> 3) == 0b010) {
                        Hash ^= Zobrist.blackPieceHash[Piece.Pawn, start];
                        Hash ^= Zobrist.blackPieceHash[Piece.Pawn, dest];
                        Hash ^= Zobrist.whitePieceHash[Piece.Pawn, dest + 8];
                        IntBoard[dest + 8] = Piece.WhitePawn;
                    }
                    else {
                        Hash ^= Zobrist.whitePieceHash[Piece.Pawn, start];
                        Hash ^= Zobrist.whitePieceHash[Piece.Pawn, dest];
                        Hash ^= Zobrist.blackPieceHash[Piece.Pawn, dest - 8];
                        IntBoard[dest - 8] = Piece.BlackPawn;
                    }
                    IntBoard[start] = IntBoard[dest];
                    IntBoard[dest] = 0;
                }
            }
            // Undoes Castle
            else if (Piece.IsType(IntBoard[dest], Piece.King) && Math.Abs(start - dest) == 2) {
                
                if (Piece.IsColor(IntBoard[dest], Piece.White)) {
                    Hash ^= Zobrist.whitePieceHash[Piece.King, start];
                    Hash ^= Zobrist.whitePieceHash[Piece.King, dest];    
                }
                else {
                    Hash ^= Zobrist.blackPieceHash[Piece.King, start];
                    Hash ^= Zobrist.blackPieceHash[Piece.King, dest];
                }
                
                capturedPiece = 0;
                IntBoard[start] = IntBoard[dest];
                IntBoard[dest] = 0;
                switch (dest)
                {
                    case 2:
                        IntBoard[0] = Piece.WhiteRook;
                        IntBoard[3] = 0;
                        Hash ^= Zobrist.whitePieceHash[Piece.Rook, 0];
                        Hash ^= Zobrist.whitePieceHash[Piece.Rook, 3];  
                        break;
                    case 6:
                        IntBoard[7] = Piece.WhiteRook;
                        IntBoard[5] = 0;
                        Hash ^= Zobrist.whitePieceHash[Piece.Rook, 7];
                        Hash ^= Zobrist.whitePieceHash[Piece.Rook, 5];
                        break;
                    case 58:
                        IntBoard[56] = Piece.BlackRook;
                        IntBoard[59] = 0;
                        Hash ^= Zobrist.blackPieceHash[Piece.Rook, 56];
                        Hash ^= Zobrist.blackPieceHash[Piece.Rook, 59];
                        break;
                    case 62:
                        IntBoard[63] = Piece.BlackRook;
                        IntBoard[61] = 0;
                        Hash ^= Zobrist.blackPieceHash[Piece.Rook, 63];
                        Hash ^= Zobrist.blackPieceHash[Piece.Rook, 61];
                        break;
                }
            }
            // Undoes all other moves
            else {
                IntBoard[start] = IntBoard[dest];
                IntBoard[dest] = capturedPiece;
                if (movingColor == Piece.White) {
                    Hash ^= Zobrist.whitePieceHash[movingPiece, start];
                    Hash ^= Zobrist.whitePieceHash[movingPiece, dest];
                    if (capturedPiece > 0) { 
                        Hash ^= Zobrist.whitePieceHash[capturedPiece & 0b111, dest];
                    }
                }
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

        public PriorityQueue<Move, int> PseudoLegalMoves()
        {
            moveList = new(50);

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
                UndoMove();
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
            // if (flag == Move.TypeEnPassant) Debug.Log("En Passant " + pos1 + " --> " + pos2);
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
    }
}