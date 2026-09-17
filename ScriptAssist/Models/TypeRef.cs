namespace HanumanInstitute.ScriptAssist;

/// <summary>
/// An opaque type identifier interpreted by a language profile.
/// </summary>
public readonly struct TypeRef : IEquatable<TypeRef>
{
    /// <summary>
    /// An unknown type; member completion stays silent.
    /// </summary>
    public static TypeRef Unknown { get; } = new("");

    /// <summary>
    /// The empty path at the start of a statement.
    /// </summary>
    public static TypeRef Root { get; } = new("root");

    /// <summary>
    /// Creates a type with the given profile-specific identifier.
    /// </summary>
    public TypeRef(string id) => _id = id ?? "";

    /// <summary>
    /// Gets the profile-specific identifier. Empty for <see cref="Unknown"/> and <c>default</c>.
    /// </summary>
    public string Id => _id ?? "";

    private readonly string _id;

    /// <summary>
    /// Gets whether the type is unknown.
    /// </summary>
    public bool IsUnknown => !Id.HasValue();

    /// <summary>
    /// Gets whether the type is the statement root.
    /// </summary>
    public bool IsRoot => Id == "root";

    /// <inheritdoc />
    public bool Equals(TypeRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TypeRef other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id);

    /// <inheritdoc />
    public override string ToString() => Id.Length == 0 ? "unknown" : Id;

    /// <summary>
    /// Compares two type references.
    /// </summary>
    public static bool operator ==(TypeRef left, TypeRef right) => left.Equals(right);

    /// <summary>
    /// Compares two type references.
    /// </summary>
    public static bool operator !=(TypeRef left, TypeRef right) => !left.Equals(right);
}
