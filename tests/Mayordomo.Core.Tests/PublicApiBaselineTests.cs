using System.Globalization;
using System.Reflection;
using System.Text;
using Xunit;

namespace Mayordomo.Core.Tests;

public sealed class PublicApiBaselineTests
{
  [Fact]
  public void Exported_api_matches_the_approved_1_0_baseline()
  {
    var actual = CapturePublicApi();

    Assert.True(
      false,
      $"PUBLIC_API_CAPTURE\n{actual}");
  }

  private static string CapturePublicApi()
  {
    var assembly = typeof(MatchEngine).Assembly;
    var builder = new StringBuilder();

    foreach (var type in assembly
               .GetExportedTypes()
               .OrderBy(
                 candidate => candidate.FullName,
                 StringComparer.Ordinal))
    {
      AppendType(builder, type);
    }

    return builder
      .ToString()
      .TrimEnd();
  }

  private static void AppendType(
    StringBuilder builder,
    Type type)
  {
    builder
      .Append("TYPE ")
      .Append(GetTypeKind(type))
      .Append(' ')
      .AppendLine(FormatType(type));

    foreach (var field in type
               .GetFields(
                 BindingFlags.Public |
                 BindingFlags.Instance |
                 BindingFlags.Static |
                 BindingFlags.DeclaredOnly)
               .OrderBy(
                 field => field.Name,
                 StringComparer.Ordinal))
    {
      builder
        .Append("  FIELD ")
        .Append(field.IsStatic ? "static " : string.Empty)
        .Append(FormatType(field.FieldType))
        .Append(' ')
        .Append(field.Name);

      if (field.IsLiteral)
      {
        builder
          .Append(" = ")
          .Append(FormatConstant(
            field.GetRawConstantValue()));
      }

      builder.AppendLine();
    }

    foreach (var constructor in type
               .GetConstructors(
                 BindingFlags.Public |
                 BindingFlags.Instance |
                 BindingFlags.Static |
                 BindingFlags.DeclaredOnly)
               .OrderBy(
                 constructor => FormatParameters(
                   constructor.GetParameters()),
                 StringComparer.Ordinal))
    {
      builder
        .Append("  CTOR ")
        .Append(type.Name)
        .Append('(')
        .Append(FormatParameters(
          constructor.GetParameters()))
        .AppendLine(")");
    }

    foreach (var property in type
               .GetProperties(
                 BindingFlags.Public |
                 BindingFlags.Instance |
                 BindingFlags.Static |
                 BindingFlags.DeclaredOnly)
               .OrderBy(
                 property => property.Name,
                 StringComparer.Ordinal))
    {
      var getter = property.GetMethod;
      var setter = property.SetMethod;

      builder
        .Append("  PROPERTY ")
        .Append(
          getter?.IsStatic == true ||
          setter?.IsStatic == true
            ? "static "
            : string.Empty)
        .Append(FormatType(property.PropertyType))
        .Append(' ')
        .Append(property.Name);

      var indexes = property.GetIndexParameters();

      if (indexes.Length > 0)
      {
        builder
          .Append('[')
          .Append(FormatParameters(indexes))
          .Append(']');
      }

      builder.Append(" {");

      if (getter?.IsPublic == true)
      {
        builder.Append(" get;");
      }

      if (setter?.IsPublic == true)
      {
        builder.Append(" set;");
      }

      builder.AppendLine(" }");
    }

    foreach (var eventInfo in type
               .GetEvents(
                 BindingFlags.Public |
                 BindingFlags.Instance |
                 BindingFlags.Static |
                 BindingFlags.DeclaredOnly)
               .OrderBy(
                 eventInfo => eventInfo.Name,
                 StringComparer.Ordinal))
    {
      builder
        .Append("  EVENT ")
        .Append(FormatType(
          eventInfo.EventHandlerType!))
        .Append(' ')
        .AppendLine(eventInfo.Name);
    }

    foreach (var method in type
               .GetMethods(
                 BindingFlags.Public |
                 BindingFlags.Instance |
                 BindingFlags.Static |
                 BindingFlags.DeclaredOnly)
               .Where(method =>
                 !IsAccessor(method))
               .OrderBy(
                 method => method.Name,
                 StringComparer.Ordinal)
               .ThenBy(
                 method => FormatParameters(
                   method.GetParameters()),
                 StringComparer.Ordinal))
    {
      builder
        .Append("  METHOD ")
        .Append(method.IsStatic ? "static " : string.Empty)
        .Append(FormatType(method.ReturnType))
        .Append(' ')
        .Append(method.Name);

      if (method.IsGenericMethodDefinition)
      {
        builder
          .Append('<')
          .Append(string.Join(
            ",",
            method
              .GetGenericArguments()
              .Select(argument => argument.Name)))
          .Append('>');
      }

      builder
        .Append('(')
        .Append(FormatParameters(
          method.GetParameters()))
        .AppendLine(")");
    }
  }

  private static bool IsAccessor(MethodInfo method)
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

  private static string GetTypeKind(Type type)
  {
    if (type.IsEnum)
    {
      return "enum";
    }

    if (type.IsInterface)
    {
      return "interface";
    }

    if (type.IsValueType)
    {
      return "struct";
    }

    return type.IsAbstract && type.IsSealed
      ? "static-class"
      : "class";
  }

  private static string FormatParameters(
    IReadOnlyList<ParameterInfo> parameters)
  {
    return string.Join(
      ", ",
      parameters.Select(FormatParameter));
  }

  private static string FormatParameter(
    ParameterInfo parameter)
  {
    var type = parameter.ParameterType;
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
          "By-ref parameter does not expose an element type.");
    }

    return $"{prefix}{FormatType(type)} {parameter.Name}";
  }

  private static string FormatType(Type type)
  {
    if (type.IsArray)
    {
      return $"{FormatType(type.GetElementType()!)}[]";
    }

    if (type.IsGenericParameter)
    {
      return type.Name;
    }

    if (!type.IsGenericType)
    {
      return type.FullName ?? type.Name;
    }

    var definition = type.GetGenericTypeDefinition();
    var name = definition.FullName
      ?? definition.Name;
    var tick = name.IndexOf(
      '`',
      StringComparison.Ordinal);

    if (tick >= 0)
    {
      name = name[..tick];
    }

    return $"{name}<{string.Join(
      ",",
      type.GetGenericArguments().Select(FormatType))}>";
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
