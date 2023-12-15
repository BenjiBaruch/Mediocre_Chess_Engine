using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace V1 {
    public static class Evaluate
    {
    // Piece Square Tables:
        /*
        - These are the piece-square tables that the evaluation function will use to adjust the value
        of pieces based on where they are on the board.
        - This can encourage the algorithm to try to put its pieces in a desirable position even if it can't see
        far enough into the future to see why these positions are beneficial.
        - Some inspiration from https://www.chessprogramming.org/Simplified_Evaluation_Function
        - Because the king should stay on the first rank in the midgame, but move towards the center in the endgame,
        the king has two piece-square tables
        - https://www.tutorialspoint.com/difference-between-constants-and-final-variables-in-java
        */
        // King Mid: Prioritize castling, stay on first rank, avoid edges if moving up
        static readonly int[] pst_kingMid = {5, 10, 5, 0, -5, 10, 5, 5,
                                            0, 0, -5, -5, -5, -5, 0, 0,
                                            -5, -10, -10, -10, -10, -10, -10, -5,
                                            -20, -18, -15, -12, -12, -15, -18, -20,
                                            -30, -27, -24, -20, -20, -24, -27, -30,
                                            -35, -32, -29, -25, -25, -29, -32, -35,
                                            -35, -32, -29, -25, -25, -29, -32, -35,
                                            -35, -32, -29, -25, -25, -29, -32, -35};
        // King End: Stay in center, avoid edges
        static readonly int[] pst_kingEnd = {-10, -10, -10, -10, -10, -10, -10, -10,
                                            -10, -7, -7, -5, -5, -7, -7, -10,
                                            -10, -7, -5, 0, 0, -5, -7, -10,
                                            -10, -5, 0, 5, 5, 0, -5, -10,
                                            -10, -5, 0, 5, 5, 0, -5, -10,
                                            -10, -7, -5, 0, 0, -5, -7, -10,
                                            -10, -7, -7, -5, -5, -7, -7, -10,
                                            -10, -10, -10, -10, -10, -10, -10, -10};
        // Queen: Prioritize center, get away from first two ranks
        static readonly int[] pst_queen = {-15, -10, -5, -5, 0, -5, -10, -15,
                                        -10, -5, -5, -5, -5, -5, -5, -5,
                                        -5, 0, 5, 5, 5, 5, 0, -5,
                                        0, 0, 5, 5, 5, 5, 0, 0,
                                        0, 0, 5, 5, 5, 5, 0, 0,
                                        -5, 0, 5, 5, 5, 5, 0, -5,
                                        0, 0, 0, 0, 0, 0, 0, 0,
                                        -5, 0, 0, 0, 0, 0, 0, -5};
        // Rook: Avoid a and h files, prioritize 7 rank
        // The values on this table were from https://www.chessprogramming.org/Simplified_Evaluation_Function
        static readonly int[] pst_rook = {0, 0, 0, 5, 5, 0, 0, 0,
                                        -5, 0, 0, 0, 0, 0, 0, -5,
                                        -5, 0, 0, 0, 0, 0, 0, -5,
                                        -5, 0, 0, 0, 0, 0, 0, -5,
                                        -5, 0, 0, 0, 0, 0, 0, -5,
                                        -5, 0, 0, 0, 0, 0, 0, -5,
                                        5, 10, 10, 10, 10, 10, 10, 5,
                                        0, 0, 0, 0, 0, 0, 0, 0};
        // Bishop: Avoid edges, really avoid corners, prioritize strong diagonals in the center
        // The values on this table were from https://www.chessprogramming.org/Simplified_Evaluation_Function
        static readonly int[] pst_bishop = {-20, -10, -10, -10, -10, -10, -10, -20,
                                            -10, 5, 0, 0, 0, 0, 5, -10,
                                            -10, 10, 10, 10, 10, 10, 10, -10,
                                            -10, 0, 10, 10, 10, 10, 0, -10,
                                            -10, 5, 5, 10, 10, 5, 5, -10,
                                            -10, 0, 5, 10, 10, 5, 0, -10,
                                            -10, 0, 0, 0, 0, 0, 0, -10,
                                            -20, -10, -10, -10, -10, -10, -10, -20};
        // Knight: Heavily penalizes corners and edges b/c knights move slowly. Prioritizes center.
        // Initial positions have a heavy penalty and ideal first move has a boost to encourage early activation
        static readonly int[] pst_knight = {-50, -30, -20, -20, -20, -20, -30, -50,
                                            -30, -10, 0, 5, 5, 0, -10, -30,
                                            -20, 3, 6, 12, 12, 6, 3, -20,
                                            -10, 0, 9, 15, 15, 9, 0, -10,
                                            -10, 3, 9, 15, 15, 9, 3, -10,
                                            -10, 0, 6, 12, 12, 6, 0, -10,
                                            -10, 0, 5, 5, 5, 5, 0, -10,
                                            -20, -10, -10, -10, -10, -10, -10, -20};

        // Add boosts to pawns on a2, b2, c2, f2, g2, h2 because those defend a castled king.
        // Penalize keeping pawns on d2, e2 and boost d4, e4 to encourage taking the center.
        // Penalize moving a pawn to 3rd rank except on a, h files because those can block a bishop.
        // Give boosts to pawns closer to promotion.
        // Values on 1st and 8th rank don't matter because there can't be a pawn on those ranks.
        static readonly int[] pst_pawn = {-999, -999, -999, -999, -999, -999, -999, -999,
                                        3, 5, 5, -10, -10, 5, 5, 3,
                                        3, -3, -5, 0, 0, -5, -3, 5,
                                        0, 0, 0, 10, 10, 0, 0, 0,
                                        3, 3, 6, 13, 13, 6, 3, 3,
                                        6, 6, 12, 16, 16, 12, 6, 6,
                                        30, 30, 30, 30, 30, 30, 30, 30,
                                        999, 999, 999, 999, 999, 999, 999, 999};

        static int GetPhase(int[] board) 
        {
            return 0;
        }

        static int MobilityRay(int[] board, int pos, int offset, int emptyBoost, int captureBoost, int defendBoost, int color) 
        {
            int boost = 0;
            if (pos + offset > -1 && pos + offset < 64 && 
                Math.Abs((pos & 0b111) - ((pos + offset) & 0b111)) < 3) {
                pos += offset;
            }
            else {
                return 0;
            }
            while (board[pos] == 0) {
                boost += emptyBoost;
                if (pos + offset > -1 && pos + offset < 64 && 
                    Math.Abs((pos & 0b111) - ((pos + offset) & 0b111)) < 3) {
                    pos += offset;
                }
                else {
                    break;
                }
            }
            if (board[pos] > 0) {
                if (Piece.IsColor(board[pos], color)) 
                    boost += defendBoost;
                else 
                    boost += captureBoost;
            }
            return boost;
        }

        static int MobilityPoint(int[] board, int pos, int dest) 
        {
            if (dest > -1 & dest < 64 && Math.Abs((pos&0b111)-(dest&0b111)) < 4) {
                if (board[dest] == -1) 
                    return 0;
                if (Piece.Color(board[pos]) == Piece.Color(board[dest])) 
                    return 2;
                else 
                    return 3;
            } 
            else 
                return -3;
        }

        public static int EvalBoard(int[] board, bool evalPov, bool mobilitySearch) 
        {
            // Help from  https://www.chessprogramming.org/Tapered_Eval
            int evaluation = 0;
            //int[] evalTable = new int[64];
            int phase = GetPhase(board);
            for (int pos = 0; pos < 64; pos++) if (board[pos] > -1) {
                int score;
                int piece = Piece.Type(board[pos]);
                int color = Piece.Color(board[pos]);
                bool whitePov = color == Piece.White;
                int pst_pos = whitePov ? pos : ((7 - (pos >> 3)) * 8 + (pos & 0b111));
                // pos >> 3 == rank of position
                // pos & 0b111 == file of position

                switch (piece) {
                    case Piece.King:
                        score = 50000 + (pst_kingMid[pst_pos] * (256 - phase) + pst_kingEnd[pst_pos] * phase) / 256;
                        break;
                    case Piece.Queen:
                        score = 900 + pst_queen[pst_pos];
                        if (mobilitySearch) {
                            // Boost score if mobile or threatening piece
                            score += MobilityRay(board, pos, -9, 1, 3, 0, color);
                            score += MobilityRay(board, pos, -8, 1, 3, 0, color);
                            score += MobilityRay(board, pos, -7, 1, 3, 0, color);
                            score += MobilityRay(board, pos, -1, 1, 3, 0, color);
                            score += MobilityRay(board, pos, 1, 1, 3, 0, color);
                            score += MobilityRay(board, pos, 7, 1, 3, 0, color);
                            score += MobilityRay(board, pos, 8, 1, 3, 0, color);
                            score += MobilityRay(board, pos, 9, 1, 3, 0, color);
                        }
                        break;
                    case Piece.Rook:
                        score = 500 + pst_rook[pst_pos];
                        if (mobilitySearch) {
                            // Boost score if mobile or threatening piece
                            score += MobilityRay(board, pos, -8, 1, 4, 1, color);
                            score += MobilityRay(board, pos, -1, 1, 4, 1, color);
                            score += MobilityRay(board, pos, 1, 1, 4, 1, color);
                            score += MobilityRay(board, pos, 8, 1, 4, 1, color);
                        }
                        break;
                    case Piece.Bishop:
                        score = 315 + pst_bishop[pst_pos];
                        if (mobilitySearch) {
                            // Boost score if mobile or threatening piece
                            score += MobilityRay(board, pos, -9, 2, 4, 2, color);
                            score += MobilityRay(board, pos, -7, 2, 4, 2, color);
                            score += MobilityRay(board, pos, 7, 2, 4, 2, color);
                            score += MobilityRay(board, pos, 9, 2, 4, 2, color);
                        }
                        break;
                    case Piece.Knight:
                        score = 320 + pst_knight[pst_pos];
                        if (mobilitySearch) {
                            // Boost score if protecting or threatening piece
                            score += MobilityPoint(board, pos, pos - 17);
                            score += MobilityPoint(board, pos, pos - 15);
                            score += MobilityPoint(board, pos, pos - 10);
                            score += MobilityPoint(board, pos, pos - 6);
                            score += MobilityPoint(board, pos, pos + 17);
                            score += MobilityPoint(board, pos, pos + 15);
                            score += MobilityPoint(board, pos, pos + 10);
                            score += MobilityPoint(board, pos, pos + 6);
                        }
                        // if (((pos >> 3) % 7) > 0 && ((pos & 0b111) % 7) > 0 &&
                        //     whitePov ? (Piece.IsType(board[pos-9], Piece.WhitePawn) || Piece.IsType(board[pos-7], Piece.WhitePawn))
                        //              : (Piece.IsType(board[pos+7], Piece.BlackPawn) || Piece.IsType(board[pos+9], Piece.BlackPawn))) {
                        //     // Boost score if at pawn outpost (i.e. is being protected by pawn)
                        //     score += 20;
                        // }
                        break;
                    case Piece.Pawn:
                        score = 85 + pst_pawn[pst_pos];
                        if (whitePov) {
                            if ((pos & 0b111) > 0 && board[pos+7] > 0) {
                                if (Piece.IsColor(board[pos+7], Piece.Black)) {
                                    score += 3;
                                }
                                else {
                                    score += 4;
                                }
                                if (!Piece.IsType(board[pos+7], Piece.Pawn)) {
                                        score += 3;
                                    }
                            }
                            if ((pos & 0b111) < 7 && board[pos+9] > 0) {
                                if (Piece.IsColor(board[pos+9], Piece.Black)) {
                                    score += 3;
                                }
                                else {
                                    score += 4;
                                }
                                if (!Piece.IsType(board[pos+9], Piece.Pawn)) {
                                        score += 3;
                                    }
                            }
                            if (board[pos+8] == Piece.WhitePawn) 
                                score -= 20;
                        } 
                        else {
                            if ((pos & 0b111) > 0 && board[pos-9] > 0) {
                                if (Piece.IsColor(board[pos-9], Piece.White)) {
                                    score += 3;
                                }
                                else {
                                    score += 4;
                                }
                                if (!Piece.IsType(board[pos-9], Piece.Pawn)) {
                                        score += 3;
                                    }
                            }
                            if ((pos & 0b111) < 7 && board[pos-7] > 0) {
                                if (Piece.IsColor(board[pos-7], Piece.White)) {
                                    score += 3;
                                }
                                else {
                                    score += 4;
                                }
                                if (!Piece.IsType(board[pos-7], Piece.Pawn)) {
                                        score += 3;
                                    }
                            }
                            if (board[pos-8] == Piece.BlackPawn) 
                                score -= 20;
                        }
                        break;
                    default:
                        score = 0;
                        break;
                }
                //evalTable[pos] = score;
                if (whitePov == evalPov) evaluation += score;
                else evaluation -= score;
            }
            return evaluation;
        }
    }
}