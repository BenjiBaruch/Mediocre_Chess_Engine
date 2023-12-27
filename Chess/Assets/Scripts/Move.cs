using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using TMPro;

public readonly struct Move : IEquatable<Move>, IEqualityComparer<Move>
{
    // Heavily inspired by Sebastian Lague's video
    // Move value is stored as 16 bit value, where:
    // - Bits 0-3 represent move type (T)
    // - Bits 4-9 represent destination tile (D)
    // - Bits 10-15 represent starting tile (S)
    // ushort bit-wise format: TTTTDDDDDDSSSSSS

    readonly ushort value; // Stores all move information

    // Move type flags
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

    // Masks determines which bits of value represent what information about move
    const ushort typeMask = 0b1111 << 12;
    const ushort destMask = 0b111111 << 6;
    const ushort startMask = 0b111111;
    
    public Move(int start, int dest, int type)
    {
        value = (ushort)(type << 12 | dest << 6 | start);
    }

    public Move(int value) { this.value = (ushort)value; }

    public int TopScore => (value >> 12) switch {
            TypePromoteToQueen => 12,
            TypePromoteToKnight => 11,
            TypeCaptureQueen => 10,
            TypeCaptureRook => 9,
            TypeCaptureBishop => 8,
            TypeCaptureKnight => 8,
            TypeEnPassant => 5,
            TypeCastle => 4,
            TypeCapturePawn => 3,
            TypePawnLeap => 2,
            TypeNormal => 1,
            _ => 0
        };
    public int MinScore => (value >> 12) switch {
            TypePromoteToQueen => 0,
            TypePromoteToKnight => 1,
            TypeCaptureQueen => 2,
            TypeCaptureRook => 3,
            TypeCaptureBishop => 4,
            TypeCaptureKnight => 5,
            TypeEnPassant => 7,
            TypeCastle => 8,
            TypeCapturePawn => 9,
            TypePawnLeap => 10,
            TypeNormal => 11,
            _ => 12
        };
    
    // Position properties
    public int Start => value & startMask;
    public int StartCol => value & 0b111;
    public int StartRow => (value & startMask) >> 3;
    public static int StatStart(int value) => value & startMask;
    public int Dest => (value & destMask) >> 6;
    public int DestCol => (value & 0b111000000) >> 6;
    public int DestRow => (value & destMask) >> 9;
    public static int StatDest(int value) => (value & destMask) >> 6;
    public Move CastlePartnerMove => Dest switch {
        2 => new Move(0, 3, TypeCastle),
        6 => new Move(7, 5, TypeCastle),
        62 => new Move(63, 61, TypeCastle),
        58 => new Move(56, 59, TypeCastle),
        _ => new Move(0, 0, 0)
    };
    // Type property
    public int Type => value >> 12;
    public static int StatType(int value) => value >> 12;
    public ushort Value => value;
    public static string TypeString(int type) => type switch {
        TypeNormal => "Normal",
        TypeCastle => "Castle",
        TypeEnPassant => "EnPassant",
        TypePawnLeap => "PawnLeap",
        TypePromoteToBishop => "PromoteToBishop",
        TypePromoteToKnight => "PromoteToKnight",
        TypePromoteToRook => "PromoteToRook",
        TypePromoteToQueen => "PromoteToQueen",
        TypeCapturePawn  => "CapturePawn",
        TypeCaptureKnight  => "CaptureKnight",
        TypeCaptureBishop  => "CaptureBishop",
        TypeCaptureRook  => "CaptureRook",
        TypeCaptureQueen  => "CaptureQueen",
        _ => "IncorrectFormat"
    };
    
    // More properties
    public bool IsPromotion { get { return Type > 3 && Type < 8; } }
    public int PromotionPieceType => Type switch {
        TypePromoteToBishop => Piece.Bishop,
        TypePromoteToRook => Piece.Rook,
        TypePromoteToKnight => Piece.Knight,
        TypePromoteToQueen => Piece.Queen,
        _ => Piece.Empty
    };
    public static bool StatIsPromotion(int type) => type > 3 && type < 8;

    public bool Equals(Move other)
    {
        return value == other.value;
    }

    public bool Equals(Move x, Move y)
    {
        return x.value == y.value;
    }

    public int GetHashCode(Move obj)
    {
        return obj.value;
    }

    public bool IsNull => value == 0;
    public bool IsEnPassant => Type == TypeEnPassant;
    public bool IsCastle => Type == TypeCastle;
    public bool IsCapture => Type == 2 || (Type >= 10 && Type <= 15);
    public bool IsNormalCapture => Type >= 10 && Type <= 15;
    public bool IsQuiet => Type < 2 || Type == 3;

    public new string ToString { get { 
        return "" + Board.LetterCodes[StartCol] + "" + (StartRow+1) + "[" + Start + "]" +
        " --> " + Board.LetterCodes[DestCol] + "" + (DestRow+1) + "[" + Dest + "]" + 
        " :: (" + value + ")"; 
        }
    }

}
