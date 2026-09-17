namespace HanumanInstitute.ScriptAssist.AviSynth;

/// <summary>
/// Decodes AviSynth <c>$Plugin!Name!Param$</c> type letters.
/// </summary>
public static class AviSynthParameters
{
    /// <summary>
    /// Decodes native parameter types and optional names; unsupported formats remain unknown.
    /// </summary>
    public static string[]? Parse(string? format)
    {
        if (format == null) { return null; }

        var result = new List<string>();
        for (var i = 0; i < format.Length; i++)
        {
            string? name = null;
            if (format[i] == '[')
            {
                var end = format.IndexOf(']', i + 1);
                if (end < 0)
                {
                    return null;
                }

                name = format[(i + 1)..end];
                i = end + 1;
                if (i >= format.Length)
                {
                    return null;
                }
            }

            var type = format[i] switch
            {
                'c' => "clip",
                'i' => "int",
                'f' => "float",
                'b' => "bool",
                's' => "string",
                '.' => "any",
                'n' => "function",
                'a' => "array",
                _ => null
            };
            if (type == null)
            {
                return null;
            }

            if (i + 1 < format.Length && format[i + 1] is '+' or '*')
            {
                type += format[++i];
            }

            result.Add(name == null ? type : type + " [" + name + "]");
        }

        return result.ToArray();
    }
}
