namespace Phantasma.VM
{
    public enum Opcode
    {
        NOP,

        // register
        MOVE,    // copy reference
        COPY,   // copy by value
        LOAD,
        PUSH,
        POP,
        SWAP,

        // flow
        CALL,
        EXTCALL,
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
        NOT,
        AND,
        OR,
        XOR,
        EQUAL,
        LT,
        GT,
        LTE,
        GTE,
        MIN,
        MAX,

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

        // context
        CTX,
        SWITCH,

        // array
        PUT,
        GET, // lookups a key and copies a reference into register
    }
}
