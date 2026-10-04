using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class PublicApiCompatibilityTests
{
  [Fact]
  public void Exported_api_matches_the_1_0_compatibility_baseline()
  {
    var snapshot = BuildSnapshot();
    var hash = Convert.ToHexString(
      SHA256.HashData(
        Encoding.UTF8.GetBytes(snapshot)));

    Assert.Equal(
      "20B4806BBD144D2B317E701BB8EF3FED656AE9E731E1D48414D53298F199E70A",
      hash);
  }

  private static string BuildSnapshot()
  {
    var lines = new List<string>();

    foreach (var type in typeof(MatchState)
               .Assembly
               .GetExportedTypes()
               .OrderBy(
                 candidate => candidate.FullName,
                 StringComparer.Ordinal))
    {
      lines.Add(FormatType(type));

      var members = new List<string>();

      members.AddRange(
        type.GetConstructors(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly)
          .Select(FormatConstructor));

      members.AddRange(
        type.GetFields(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly)
          .Select(FormatField));

      members.AddRange(
        type.GetProperties(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly)
          .Select(FormatProperty));

      members.AddRange(
        type.GetEvents(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly)
          .Select(FormatEvent));

      members.AddRange(
        type.GetMethods(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly)
          .Where(method =>
            !IsAccessor(method))
          .Select(FormatMethod));

      foreach (var member in members
                 .OrderBy(
                   value => value,
                   StringComparer.Ordinal))
      {
        lines.Add($"  {member}");
      }
    }

    return string.Join(
      "\n",
      lines);
  }

  private static string FormatType(Type type)
  {
    var modifiers = new List<string>();

    if (type.IsAbstract)
    {
      modifiers.Add("abstract");
    }

    if (type.IsSealed)
    {
      modifiers.Add("sealed");
    }

    var kind = type.IsInterface
      ? "interface"
      : type.IsEnum
        ? "enum"
        : type.IsValueType
          ? "struct"
          : "class";

    var inheritance = new List<string>();

    if (type.BaseType is not null &&
        type.BaseType != typeof(object) &&
        type.BaseType != typeof(ValueType) &&
        type.BaseType != typeof(Enum))
    {
      inheritance.Add(
        TypeName(type.BaseType));
    }

    inheritance.AddRange(
      type.GetInterfaces()
        .Select(TypeName)
        .OrderBy(
          value => value,
          StringComparer.Ordinal));

    var prefix = modifiers.Count == 0
      ? string.Empty
      : $"{string.Join(" ", modifiers)} ";

    return inheritance.Count == 0
      ? $"type {prefix}{kind} {TypeName(type)}"
      : $"type {prefix}{kind} {TypeName(type)} : {string.Join(", ", inheritance)}";
  }

  private static string FormatConstructor(
    ConstructorInfo constructor)
  {
    return $"ctor {TypeName(constructor.DeclaringType!)}" +
      $"({FormatParameters(constructor.GetParameters())})";
  }

  private static string FormatField(
    FieldInfo field)
  {
    var modifiers = new List<string>();

    if (field.IsStatic)
    {
      modifiers.Add("static");
    }

    if (field.IsLiteral)
    {
      modifiers.Add("const");
    }
    else if (field.IsInitOnly)
    {
      modifiers.Add("readonly");
    }

    var prefix = modifiers.Count == 0
      ? string.Empty
      : $"{string.Join(" ", modifiers)} ";

    var value = field.IsLiteral
      ? $" = {FormatConstant(field.GetRawConstantValue())}"
      : string.Empty;

    return $"field {prefix}{TypeName(field.FieldType)} {field.Name}{value}";
  }

  private static string FormatProperty(
    PropertyInfo property)
  {
    var getter = property.GetMethod;
    var setter = property.SetMethod;
    var modifier =
      getter?.IsStatic == true ||
      setter?.IsStatic == true
        ? "static "
        : string.Empty;

    var indexParameters =
      property.GetIndexParameters();
    var indexer = indexParameters.Length == 0
      ? string.Empty
      : $"[{FormatParameters(indexParameters)}]";

    var accessors = string.Concat(
      getter?.IsPublic == true
        ? "get;"
        : string.Empty,
      setter?.IsPublic == true
        ? "set;"
        : string.Empty);

    return $"property {modifier}{TypeName(property.PropertyType)} " +
      $"{property.Name}{indexer} {{{accessors}}}";
  }

  private static string FormatEvent(
    EventInfo eventInfo)
  {
    var accessor =
      eventInfo.AddMethod ??
      eventInfo.RemoveMethod;
    var modifier = accessor?.IsStatic == true
      ? "static "
      : string.Empty;

    return $"event {modifier}{TypeName(eventInfo.EventHandlerType!)} {eventInfo.Name}";
  }

  private static string FormatMethod(
    MethodInfo method)
  {
    var modifier = method.IsStatic
      ? "static "
      : string.Empty;
    var generic = method.IsGenericMethodDefinition
      ? $"<{string.Join(
          ",",
          method.GetGenericArguments()
            .Select(argument => argument.Name))}>"
      : string.Empty;

    return $"method {modifier}{TypeName(method.ReturnType)} " +
      $"{method.Name}{generic}({FormatParameters(method.GetParameters())})";
  }

  private static bool IsAccessor(
    MethodInfo method)
  {
    if (!method.IsSpecialName)
    {
      return false;
    }

    return method.Name.StartsWith(
             "get_",
             StringComparison.Ordinal) ||
           method.Name.StartsWith(
             "set_",
             StringComparison.Ordinal) ||
           method.Name.StartsWith(
             "add_",
             StringComparison.Ordinal) ||
           method.Name.StartsWith(
             "remove_",
             StringComparison.Ordinal);
  }

  private static string FormatParameters(
    IReadOnlyList<ParameterInfo> parameters)
  {
    return string.Join(
      ", ",
      parameters.Select(
        parameter =>
        {
          var type =
            parameter.ParameterType;
          var prefix = string.Empty;

          if (type.IsByRef)
          {
            prefix = parameter.IsOut
              ? "out "
              : parameter.IsIn
                ? "in "
                : "ref ";

            type = type.GetElementType()
              ?? throw new InvalidOperationException(
                "By-ref parameter has no element type.");
          }

          var optional = parameter.HasDefaultValue
            ? $" = {FormatConstant(parameter.DefaultValue)}"
            : string.Empty;

          return $"{prefix}{TypeName(type)} {parameter.Name}{optional}";
        }));
  }

  private static string TypeName(Type type)
  {
    if (type.IsArray)
    {
      return $"{TypeName(type.GetElementType()!)}[]";
    }

    if (type.IsGenericParameter)
    {
      return type.Name;
    }

    if (!type.IsGenericType)
    {
      return (type.FullName ?? type.Name)
        .Replace('+', '.');
    }

    var definition =
      type.GetGenericTypeDefinition();
    var name = (
      definition.FullName ??
      definition.Name)
      .Replace('+', '.');
    var tick = name.IndexOf(
      '`',
      StringComparison.Ordinal);

    if (tick >= 0)
    {
      name = name[..tick];
    }

    return $"{name}<" +
      string.Join(
        ",",
        type.GetGenericArguments()
          .Select(TypeName)) +
      ">";
  }

  private static string FormatConstant(
    object? value)
  {
    return value switch
    {
      null => "null",
      string text => $"\"{text}\"",
      char character => $"'{character}'",
      bool boolean => boolean
        ? "true"
        : "false",
      IFormattable formattable =>
        formattable.ToString(
          null,
          CultureInfo.InvariantCulture),
      _ => value.ToString()
        ?? string.Empty
    };
  }
}
