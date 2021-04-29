namespace ArcCore.Components
{
    [System.Obsolete]
    public class CutoffUtils
    {
        public static readonly ShaderCutoff Unjudged = new ShaderCutoff() { Value = 1f };
        public static readonly ShaderCutoff JudgedP  = new ShaderCutoff() { Value = 0f };
        public static readonly ShaderCutoff JudgedL  = new ShaderCutoff() { Value = 2f };
    }
}
