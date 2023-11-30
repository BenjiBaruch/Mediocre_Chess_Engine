using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

public readonly struct Move
{
    // Heavily inspired by Sebastian Lague's video
    // Move value is stored as 16 bit value, where:
    // - Bits 0-3 represent move type (T)
    // - Bits 4-9 represent destination tile (D)
    // - Bits 10-15 represent starting tile (S)
    // ushort format: TTTTDDDDDDSSSSSS

    readonly ushort value;

    public const int TypeNormal = 0;
    public const int TypeCastle = 1;
    public const int TypeEnPassant = 2;
    public const int TypePawnLeap = 3;
    public const int TypePromoteToBishop = 4;
    public const int TypePromoteToKnight = 5;
    public const int TypePromoteToRook = 6;
    public const int TypePromoteToQueen = 7;
    public const int TypeCapturePawn = 10;
    public const int TypeCaptureKnight = 11;
    public const int TypeCaptureBishop = 13;
    public const int TypeCaptureRook = 14;
    public const int TypeCaptureQueen = 15;

    const ushort typeMask = 0b1111 << 12;
    const ushort destMask = 0b111111 << 6;
    const ushort startMask = 0b111111;
    
    public Move(int start, int dest, int type)
    {
        value = (ushort)(type << 12 | dest << 6 | start);
    }
    

    public int Start => value & startMask;
    public int StartCol => value & 0b111;
    public int StartRow => (value & startMask) >> 3;
    public int Dest => (value & destMask) >> 6;
    public int DestCol => (value & 0b111000000) >> 6;
    public int DestRow => (value & destMask) >> 9;
    public int Type => value >> 12;
    public ushort Value => value;
    
    public bool IsPromotion { get { return Type > 3 && Type < 8; } }
    public bool IsNull { get { return value == 0; } }
    public bool IsEnPassant { get { return Type == TypeEnPassant; } }
    public bool IsCastle { get { return Type == TypeCastle; } }

    public new string ToString { get { 
        return "" + Board.LetterCodes[StartCol] + (StartRow+1) + " --> " + Board.LetterCodes[DestCol] + (DestRow+1); 
        }
    }

}
