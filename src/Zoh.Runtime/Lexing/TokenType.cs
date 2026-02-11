namespace Zoh.Runtime.Lexing;

/// <summary>
/// All token types recognized by the ZOH lexer.
/// </summary>
public enum TokenType
{
    // End of file
    Eof,

    // Error recovery
    Error,

    // Virtual tokens
    StoryNameEnd,
    CheckpointEnd,

    // === Literals ===
    Integer,
    Double,
    String,
    MultilineString,
    Expression,        // Content between backticks

    // === Keywords ===
    True,
    False,
    Nothing,

    // === Identifiers ===
    Identifier,

    // === Punctuation ===
    Slash,             // /
    SlashSemicolon,    // /;  (block end)
    Semicolon,         // ;
    Comma,             // ,
    Colon,             // :
    At,                // @
    Star,              // * (renamed from Asterisk)
    StarStar,          // **
    Hash,              // #
    Dot,               // .

    // === Brackets ===
    LeftBracket,       // [
    RightBracket,      // ]
    LeftBrace,         // {
    RightBrace,        // }
    LeftParen,         // (
    RightParen,        // )
    LeftAngle,         // <
    RightAngle,        // >

    // === Operators ===
    Plus,              // +
    Minus,             // -
    Percent,           // %

    Equal,             // =
    EqualEqual,        // ==
    Bang,              // !
    BangEqual,         // !=

    LessEqual,         // <=
    GreaterEqual,      // >=

    AmpersandAmpersand,// &&
    PipePipe,          // ||
    Pipe,              // |

    ArrowLeft,         // <-
    ArrowRight,        // ->

    // === Sugar tokens ===
    Jump,              // ====>
    Fork,              // ====+
    Call,              // <===+
    StorySeparator,    // ===

    // === Interpolation sugar ===
    Backtick,          // `
    SlashBacktick,     // /`
    SlashQuote,        // /" or /'
    DollarParen,       // $(
    DollarHashParen,   // $#(
    DollarQuestionParen, // $?(
    DollarString,      // $"
    DollarRef,         // $*

    // === Channel ===
    Channel,           // <name> as single token
}
