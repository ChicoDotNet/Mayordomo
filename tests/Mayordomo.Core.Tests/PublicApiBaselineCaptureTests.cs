using System.Globalization;
using System.Reflection;
using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class PublicApiBaselineCaptureTests
{
  [Fact]
  public void Capture_public_API_surface_for_1_0()
  {
    var snapshot = BuildSnapshot();

    Assert.Fail(
      $"PUBLIC_API_CAPTURE_V1\n{snapshot}");
  }

  private static string BuildSnapshot()
  {
    var assembly = typeof(MatchState).Assembly;
    var lines = new List<string>();

    foreach (var type in assembly
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
        type.GetProperties(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly)
          .Select(FormatProperty));

      members.AddRange(
        type.GetFields(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly)
          .Select(FormatField));

      members.AddRange(
        type.GetMethods(
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.DeclaredOnly)
          .Where(method =>
            !method.IsSpecialName)
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
      inheritance.Add(TypeName(type.BaseType));
    }

    inheritance.AddRange(
      type.GetInterfaces()
        .Select(TypeName)
        .OrderBy(
          value => value,
          StringComparer.Ordinal));

    return inheritance.Count == 0
      ? $"type {kind} {TypeName(type)}"
      : $"type {kind} {TypeName(type)} : {string.Join(", ", inheritance)}";
  }

  private static string FormatConstructor(
    ConstructorInfo constructor)
  {
    return $"ctor {TypeName(constructor.DeclaringType!)}" +
      $"({FormatParameters(constructor.GetParameters())})";
  }

  private static string FormatProperty(
    PropertyInfo property)
  {
    var accessors = string.Concat(
      property.GetMethod?.IsPublic == true
        ? "get;"
        : string.Empty,
      property.SetMethod?.IsPublic == true
        ? "set;"
        : string.Empty);

    var indexParameters =
      property.GetIndexParameters();

    var indexer = indexParameters.Length == 0
      ? string.Empty
      : $"[{FormatParameters(indexParameters)}]";

    var modifier =
      property.GetMethod?.IsStatic == true ||
      property.SetMethod?.IsStatic == true
        ? "static "
        : string.Empty;

    return $"property {modifier}{TypeName(property.PropertyType)} " +
      $"{property.Name}{indexer} {{{accessors}}}";
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

    var value = field.IsLiteral
      ? $" = {FormatConstant(field.GetRawConstantValue())}"
      : string.Empty;

    var prefix = modifiers.Count == 0
      ? string.Empty
      : $"{string.Join(" ", modifiers)} ";

    return $"field {prefix}{TypeName(field.FieldType)} {field.Name}{value}";
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

  private static string FormatParameters(
    IReadOnlyList<ParameterInfo> parameters)
  {
    return string.Join(
      ", ",
      parameters.Select(parameter =>
      {
        var modifier = parameter.ParameterType.IsByRef
          ? parameter.IsOut
            ? "out "
            : parameter.IsIn
              ? "in "
              : "ref "
          : string.Empty;

        return $"{modifier}{TypeName(parameter.ParameterType)} {parameter.Name}";
      }));
  }

  private static string TypeName(Type type)
  {
    if (type.IsByRef)
    {
      return TypeName(type.GetElementType()!);
    }

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

    var definition = type
      .GetGenericTypeDefinition();
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
      _ => value.ToString() ?? string.Empty
    };
  }
}
