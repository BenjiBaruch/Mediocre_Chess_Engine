using System;
using System.Collections.Generic;
using System.Text;

public struct BoardStruct : IEquatable<BoardStruct> {
    public int[] IntBoard { get; set; }
    public bool WhiteToMove { get; set; }
    public int CastlingRights { get; set; }
    public int PawnLeapFile { get; set; }
    public int HalfmoveClock { get; set; }

    public BoardStruct(int[] intBoard, bool whiteToMove, int castlingRights, int pawnLeapFile, int halfmoveClock)
    {
        IntBoard = (int[]) intBoard.Clone();
        WhiteToMove = whiteToMove;
        CastlingRights = castlingRights;
        PawnLeapFile = pawnLeapFile;
        HalfmoveClock = halfmoveClock;
    }

    public override readonly string ToString()
    {
        StringBuilder str = new(74);
        for (int row = 0; row < 8; row++) {
            for (int col = 0; col < 8; col++) {
                str.Append(Piece.Code(IntBoard[row*8+col]));
                str.Append(' ');
            } str.Append('\n');
        }
        str.Append(WhiteToMove ? "White to move" : "Black to move");
        return str.ToString();
    }

    public readonly bool Equals(BoardStruct other)
    {
        bool identical = true;
        for (int i = 0; i < 64; i++)
            identical &= IntBoard[i] == other.IntBoard[i];
        return identical && WhiteToMove == other.WhiteToMove
                         && CastlingRights == other.CastlingRights
                         && PawnLeapFile == other.PawnLeapFile
                         && HalfmoveClock == other.HalfmoveClock;
    }
}