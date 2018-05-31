using System;

namespace Phantasma.VM
{
    public enum Opcode
    {
        NOP,

        // register
        COPY,
        LOAD,
        PUSH,
        POP,
        SWAP,

        // flow
        CALL,
        JMP,
        JMPIF,
        JMPNOT,
        RET,

        // data
        CAT,
        SUBSTR,
        LEFT,
        RIGHT,
        SIZE,

        // logical
        INVERT,
        AND,
        OR,
        XOR,
        EQUAL,

        // numeric
        INC,
        DEC,
        SIGN,
        NEGATE,
        ABS,
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        SHL,
        SHR,
        LT,
        GT,
        LTE,
        GTE,
        MIN,
        MAX
    }
}
