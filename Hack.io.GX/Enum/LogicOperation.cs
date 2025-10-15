namespace Hack.io.GX;

public enum GXLogicOperation : byte
{
    CLEAR = 0,
    AND = 1,
    REVERSEAND = 2,
    COPY = 3,
    INVERSEAND = 4,
    NOOPERATION = 5,
    XOR = 6,
    OR = 7,
    NOR = 8,
    EQUIV = 9,
    INVERSE = 10,
    REVERSEOR = 11,
    INVERSECOPY = 12,
    INVERSEOR = 13,
    NAND = 14,
    SET = 15,
};