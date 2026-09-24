namespace Voyager
{
    /// <summary>Operador de comparación usado por los Requirements.</summary>
    public enum Comparison
    {
        GreaterOrEqual, // >=
        Greater,        // >
        Equal,          // ==
        NotEqual,       // !=
        LessOrEqual,    // <=
        Less            // <
    }

    /// <summary>Cómo se combinan varios requisitos de una lista.</summary>
    public enum LogicMode
    {
        All, // AND: todos deben cumplirse
        Any  // OR: alcanza con uno
    }

    public static class ComparisonExtensions
    {
        public static bool Compare(this Comparison op, int a, int b)
        {
            switch (op)
            {
                case Comparison.GreaterOrEqual: return a >= b;
                case Comparison.Greater:        return a > b;
                case Comparison.Equal:          return a == b;
                case Comparison.NotEqual:       return a != b;
                case Comparison.LessOrEqual:    return a <= b;
                case Comparison.Less:           return a < b;
                default:                        return false;
            }
        }

        public static string Symbol(this Comparison op)
        {
            switch (op)
            {
                case Comparison.GreaterOrEqual: return ">=";
                case Comparison.Greater:        return ">";
                case Comparison.Equal:          return "==";
                case Comparison.NotEqual:       return "!=";
                case Comparison.LessOrEqual:    return "<=";
                case Comparison.Less:           return "<";
                default:                        return "?";
            }
        }

        /// <summary>
        /// Cuántos elementos hacen falta como mínimo para que "cantidad OP valor" pueda cumplirse.
        /// Lo usa el mazo para garantizar que una condición diaria no sea imposible (decisión D4).
        /// </summary>
        public static int MinimumCountNeeded(this Comparison op, int value)
        {
            switch (op)
            {
                case Comparison.GreaterOrEqual:
                case Comparison.Equal:   return value > 0 ? value : 0;
                case Comparison.Greater: return value + 1 > 0 ? value + 1 : 0;
                case Comparison.NotEqual: return value == 0 ? 1 : 0;
                default:                 return 0;
            }
        }
    }
}
