using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SignatureDumper
{
    public class TypeSignatureInfo
    {
        public string Namespace { get; set; }
        public string Name { get; set; }
        public string RawTypeName { get; set; }
        /// <summary>중첩 타입이면 바깥 타입 체인 (예: "Outer+Inner"), 아니면 null.</summary>
        public string DeclaringTypeName { get; set; }
        public string FullName { get; set; }
        public string Kind { get; set; } // class, struct, interface, enum, delegate
        public string AccessModifiers { get; set; }
        public string BaseType { get; set; }
        public List<string> Interfaces { get; set; } = new List<string>();
        public List<string> EnumMembers { get; set; } = new List<string>();
        public List<string> Fields { get; set; } = new List<string>();
        public List<string> Properties { get; set; } = new List<string>();
        public List<string> Events { get; set; } = new List<string>();
        public List<string> Constructors { get; set; } = new List<string>();
        public List<string> Methods { get; set; } = new List<string>();
    }

    public class SignatureDumperOptions
    {
        public string GameDirectory { get; set; } = @"H:\steam\steamapps\common\DEFLATE";
        public string TargetAssemblyPath { get; set; }
        public string OutputDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
        public bool DumpCSharpSignatures { get; set; } = false;
        public bool DumpJsonMetadata { get; set; } = false;
        public bool DumpSummaryText { get; set; } = false;
        public bool DumpIndividualFiles { get; set; } = true;
        public string DecompileFolderName { get; set; } = "Decompiled";
    }

    public class AssemblySignatureDumper
    {
        private readonly SignatureDumperOptions _options;

        public AssemblySignatureDumper(SignatureDumperOptions options)
        {
            _options = options ?? new SignatureDumperOptions();
        }

        public void Dump()
        {
            string assemblyPath = _options.TargetAssemblyPath;

            if (string.IsNullOrEmpty(assemblyPath))
            {
                string il2cppPath = Path.Combine(_options.GameDirectory, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll");
                string managedPath = Directory.Exists(_options.GameDirectory)
                    ? Directory.GetDirectories(_options.GameDirectory, "*_Data")
                        .Select(dataDir => Path.Combine(dataDir, "Managed", "Assembly-CSharp.dll"))
                        .FirstOrDefault(File.Exists)
                    : null;

                if (File.Exists(il2cppPath))
                {
                    assemblyPath = il2cppPath;
                }
                else if (managedPath != null)
                {
                    assemblyPath = managedPath;
                }
                else
                {
                    throw new FileNotFoundException($"Assembly-CSharp.dll not found in default paths: '{il2cppPath}' or '<GameDir>\\*_Data\\Managed\\Assembly-CSharp.dll'");
                }
            }

            Console.WriteLine($"[SignatureDumper] Target Assembly: {assemblyPath}");
            string searchDir = Path.GetDirectoryName(assemblyPath);

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    var asmName = new AssemblyName(args.Name).Name + ".dll";
                    var candidatePaths = new List<string>
                    {
                        Path.Combine(searchDir, asmName),
                        Path.Combine(_options.GameDirectory, "MelonLoader", "net6", asmName),
                        Path.Combine(_options.GameDirectory, "MelonLoader", "net472", asmName),
                        Path.Combine(_options.GameDirectory, "MelonLoader", "net35", asmName),
                        Path.Combine(_options.GameDirectory, "MelonLoader", "Dependencies", asmName),
                        Path.Combine(_options.GameDirectory, "MelonLoader", "Dependencies", "Il2CppAssemblyGenerator", "Cpp2IL", "cpp2il_out", asmName)
                    };

                    if (Directory.Exists(_options.GameDirectory))
                    {
                        foreach (var dataDir in Directory.GetDirectories(_options.GameDirectory, "*_Data"))
                        {
                            candidatePaths.Add(Path.Combine(dataDir, "Managed", asmName));
                        }
                    }

                    foreach (var path in candidatePaths)
                    {
                        if (File.Exists(path))
                        {
                            return Assembly.LoadFrom(path);
                        }
                    }
                }
                catch { }
                return null;
            };

            Assembly asm = Assembly.LoadFrom(assemblyPath);
            Type[] types;

            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
                Console.WriteLine($"[SignatureDumper] Warning: Loaded {types.Length} types with some unresolved dependencies.");
            }

            Console.WriteLine($"[SignatureDumper] Analyzing {types.Length} types...");

            List<TypeSignatureInfo> infos = new List<TypeSignatureInfo>();

            foreach (var type in types.OrderBy(t => t.Namespace).ThenBy(t => t.Name))
            {
                try
                {
                    infos.Add(AnalyzeType(type));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SignatureDumper] Error analyzing type '{type.FullName}': {ex.Message}");
                }
            }

            Directory.CreateDirectory(_options.OutputDirectory);
            string asmBaseName = Path.GetFileNameWithoutExtension(assemblyPath);

            if (_options.DumpCSharpSignatures)
            {
                string csPath = Path.Combine(_options.OutputDirectory, $"{asmBaseName}_Signatures.cs");
                WriteCSharpSignatures(csPath, asmBaseName, infos);
                Console.WriteLine($"[SignatureDumper] Dumped single C# signature file to: {csPath}");
            }

            if (_options.DumpIndividualFiles)
            {
                string decompileDir = Path.Combine(_options.OutputDirectory, _options.DecompileFolderName);
                WriteIndividualFiles(decompileDir, infos);
                Console.WriteLine($"[SignatureDumper] Generated {infos.Count} individual C# files in folder: {decompileDir}");
            }

            if (_options.DumpSummaryText)
            {
                string summaryPath = Path.Combine(_options.OutputDirectory, $"{asmBaseName}_Summary.txt");
                WriteSummaryText(summaryPath, asmBaseName, infos);
                Console.WriteLine($"[SignatureDumper] Dumped Summary to: {summaryPath}");
            }

            if (_options.DumpJsonMetadata)
            {
                string jsonPath = Path.Combine(_options.OutputDirectory, $"{asmBaseName}_Signatures.json");
                WriteJsonMetadata(jsonPath, infos);
                Console.WriteLine($"[SignatureDumper] Dumped JSON metadata to: {jsonPath}");
            }
        }

        private TypeSignatureInfo AnalyzeType(Type type)
        {
            var info = new TypeSignatureInfo
            {
                Namespace = type.Namespace ?? "<global>",
                Name = GetTypeName(type, qualifyIl2CppSystem: false),
                RawTypeName = type.Name,
                DeclaringTypeName = GetDeclaringChain(type),
                FullName = type.FullName ?? type.Name
            };

            if (type.IsPublic || type.IsNestedPublic) info.AccessModifiers = "public";
            else if (type.IsNestedPrivate) info.AccessModifiers = "private";
            else if (type.IsNestedFamily) info.AccessModifiers = "protected";
            else if (type.IsNestedAssembly || type.IsNotPublic) info.AccessModifiers = "internal";
            else if (type.IsNestedFamORAssem) info.AccessModifiers = "protected internal";
            else if (type.IsNestedFamANDAssem) info.AccessModifiers = "private protected";
            else info.AccessModifiers = "internal";

            if (type.IsEnum)
            {
                info.Kind = "enum";
                foreach (var name in Enum.GetNames(type))
                {
                    try
                    {
                        var val = Convert.ChangeType(Enum.Parse(type, name), Enum.GetUnderlyingType(type));
                        info.EnumMembers.Add($"{name} = {val}");
                    }
                    catch
                    {
                        info.EnumMembers.Add(name);
                    }
                }
                return info;
            }

            if (type.IsInterface) info.Kind = "interface";
            else if (typeof(Delegate).IsAssignableFrom(type)) info.Kind = "delegate";
            else if (type.IsValueType) info.Kind = "struct";
            else info.Kind = "class";

            if (type.IsAbstract && type.IsSealed) info.AccessModifiers += " static";
            else if (type.IsAbstract && !type.IsInterface) info.AccessModifiers += " abstract";
            else if (type.IsSealed && !type.IsValueType) info.AccessModifiers += " sealed";

            if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
            {
                info.BaseType = GetTypeName(type.BaseType);
            }

            foreach (var iface in type.GetInterfaces())
            {
                info.Interfaces.Add(GetTypeName(iface));
            }

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var field in type.GetFields(flags))
            {
                if (field.IsSpecialName) continue;
                string mods = GetFieldModifiers(field);
                string fType = GetTypeName(field.FieldType);
                string val = "";
                if (field.IsLiteral && !field.IsInitOnly)
                {
                    try { val = $" = {FormatLiteral(field.GetRawConstantValue())}"; } catch { }
                }
                info.Fields.Add($"{mods} {fType} {field.Name}{val};");
            }

            foreach (var prop in type.GetProperties(flags))
            {
                string pType = GetTypeName(prop.PropertyType);
                var getMethod = prop.GetGetMethod(true);
                var setMethod = prop.GetSetMethod(true);

                string getStr = getMethod != null ? (getMethod.IsPublic ? "get; " : $"{GetMethodAccess(getMethod)} get; ") : "";
                string setStr = setMethod != null ? (setMethod.IsPublic ? "set; " : $"{GetMethodAccess(setMethod)} set; ") : "";
                string access = getMethod != null ? GetMethodAccess(getMethod) : (setMethod != null ? GetMethodAccess(setMethod) : "public");
                if ((getMethod ?? setMethod)?.IsStatic == true) access += " static";

                info.Properties.Add($"{access} {pType} {prop.Name} {{ {getStr}{setStr}}}");
            }

            foreach (var ev in type.GetEvents(flags))
            {
                string eType = GetTypeName(ev.EventHandlerType);
                info.Events.Add($"public event {eType} {ev.Name};");
            }

            foreach (var ctor in type.GetConstructors(flags))
            {
                string access = GetMethodAccess(ctor);
                string paramsStr = string.Join(", ", ctor.GetParameters().Select(FormatParameter));
                info.Constructors.Add($"{access} {info.Name}({paramsStr});");
            }

            foreach (var method in type.GetMethods(flags))
            {
                if (method.IsSpecialName) continue;
                string access = GetMethodAccess(method);
                string mods = GetMethodModifiers(method);
                string retType = GetTypeName(method.ReturnType);
                string paramsStr = string.Join(", ", method.GetParameters().Select(FormatParameter));
                string genArgs = method.IsGenericMethod ? $"<{string.Join(", ", method.GetGenericArguments().Select(t => t.Name))}>" : "";

                info.Methods.Add($"{access}{mods} {retType} {method.Name}{genArgs}({paramsStr});");
            }

            return info;
        }

        private string FormatParameter(ParameterInfo p)
        {
            string prefix = "";
            if (p.IsOut) prefix = "out ";
            else if (p.ParameterType.IsByRef) prefix = "ref ";
            else if (p.GetCustomAttributes(typeof(ParamArrayAttribute), false).Any()) prefix = "params ";

            string typeName = GetTypeName(p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType);
            string def = "";

            if (p.HasDefaultValue)
            {
                def = $" = {FormatLiteral(p.DefaultValue)}";
            }

            return $"{prefix}{typeName} {p.Name}{def}";
        }

        private string FormatLiteral(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return $"\"{EscapeJson(s)}\"";
                case char c: return $"'{EscapeJson(c.ToString())}'";
                case bool b: return b ? "true" : "false";
                case IFormattable f: return f.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }

        private string GetFieldModifiers(FieldInfo f)
        {
            string access = f.IsPublic ? "public"
                : f.IsPrivate ? "private"
                : f.IsFamily ? "protected"
                : f.IsFamilyOrAssembly ? "protected internal"
                : f.IsFamilyAndAssembly ? "private protected"
                : "internal";
            if (f.IsLiteral) return $"{access} const";
            if (f.IsStatic && f.IsInitOnly) return $"{access} static readonly";
            if (f.IsStatic) return $"{access} static";
            if (f.IsInitOnly) return $"{access} readonly";
            return access;
        }

        private string GetMethodAccess(MethodBase m)
        {
            if (m.IsPublic) return "public";
            if (m.IsPrivate) return "private";
            if (m.IsFamily) return "protected";
            if (m.IsAssembly) return "internal";
            if (m.IsFamilyOrAssembly) return "protected internal";
            if (m.IsFamilyAndAssembly) return "private protected";
            return "internal";
        }

        private string GetMethodModifiers(MethodBase m)
        {
            var parts = new List<string>();
            if (m.IsStatic) parts.Add("static");

            // 베이스 클래스 메서드를 재정의한 경우 virtual이 아니라 override로 표기
            bool isOverride = m is MethodInfo mi && mi.IsVirtual &&
                mi.GetBaseDefinition().DeclaringType != mi.DeclaringType;

            if (m.IsAbstract) parts.Add(isOverride ? "abstract override" : "abstract");
            else if (isOverride) parts.Add(m.IsFinal ? "sealed override" : "override");
            else if (m.IsVirtual && !m.IsFinal) parts.Add("virtual");
            return parts.Count > 0 ? " " + string.Join(" ", parts) : "";
        }

        private string GetDeclaringChain(Type t)
        {
            if (!t.IsNested) return null;
            var chain = new List<string>();
            for (var d = t.DeclaringType; d != null; d = d.DeclaringType)
            {
                chain.Insert(0, d.Name);
            }
            return string.Join("+", chain);
        }

        private string GetTypeName(Type t) => GetTypeName(t, qualifyIl2CppSystem: true);

        /// <param name="qualifyIl2CppSystem">false면 선언부(타입 자신의 이름)용으로 네임스페이스를 붙이지 않는다.</param>
        private string GetTypeName(Type t, bool qualifyIl2CppSystem)
        {
            if (t == null) return "void";
            if (t == typeof(void)) return "void";
            if (t == typeof(int)) return "int";
            if (t == typeof(long)) return "long";
            if (t == typeof(short)) return "short";
            if (t == typeof(byte)) return "byte";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(string)) return "string";
            if (t == typeof(object)) return "object";

            if (t.IsGenericParameter) return t.Name;
            if (t.IsByRef) return GetTypeName(t.GetElementType());
            if (t.IsArray) return $"{GetTypeName(t.GetElementType())}[{new string(',', t.GetArrayRank() - 1)}]";

            // Il2CppInterop은 mscorlib 타입을 Il2CppSystem.* 로 옮겨 생성한다.
            // 이름만 쓰면 System.Collections.Generic.List / System.Action 과 구분이 안 되므로 네임스페이스까지 남긴다.
            string prefix = qualifyIl2CppSystem && t.Namespace != null && t.Namespace.StartsWith("Il2CppSystem", StringComparison.Ordinal)
                ? t.Namespace + "."
                : "";

            if (t.IsGenericType)
            {
                string genericName = t.Name.Split('`')[0];
                var typeArgs = string.Join(", ", t.GetGenericArguments().Select(GetTypeName));
                return $"{prefix}{genericName}<{typeArgs}>";
            }

            return prefix + t.Name;
        }

        private void WriteIndividualFiles(string decompileDir, List<TypeSignatureInfo> infos)
        {
            if (Directory.Exists(decompileDir))
            {
                Directory.Delete(decompileDir, true);
            }
            Directory.CreateDirectory(decompileDir);

            foreach (var info in infos)
            {
                string nsPath = info.Namespace == "<global>" || string.IsNullOrEmpty(info.Namespace) ? "Global" : info.Namespace.Replace('.', Path.DirectorySeparatorChar);
                string targetDir = Path.Combine(decompileDir, nsPath);
                Directory.CreateDirectory(targetDir);

                // 중첩 타입은 바깥 타입 이름을 붙인다 (예: 클래스마다 있는 __c 가 한 파일에 덮어써지는 것 방지)
                string baseName = info.RawTypeName ?? info.Name;
                if (!string.IsNullOrEmpty(info.DeclaringTypeName)) baseName = $"{info.DeclaringTypeName}+{baseName}";
                string safeFileName = SanitizeFileName(baseName) + ".cs";
                string filePath = Path.Combine(targetDir, safeFileName);

                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    writer.WriteLine("using System;");
                    writer.WriteLine("using System.Collections.Generic;");
                    writer.WriteLine("using UnityEngine;");
                    writer.WriteLine();

                    string nsHeader = info.Namespace == "<global>" || string.IsNullOrEmpty(info.Namespace) ? "Global" : info.Namespace;
                    writer.WriteLine($"namespace {nsHeader}");
                    writer.WriteLine("{");

                    if (!string.IsNullOrEmpty(info.DeclaringTypeName))
                    {
                        writer.WriteLine($"    // Nested in: {info.DeclaringTypeName}");
                    }

                    string baseClause = "";
                    var bases = new List<string>();
                    if (!string.IsNullOrEmpty(info.BaseType)) bases.Add(info.BaseType);
                    if (info.Interfaces != null && info.Interfaces.Count > 0) bases.AddRange(info.Interfaces);
                    if (bases.Count > 0) baseClause = $" : {string.Join(", ", bases)}";

                    writer.WriteLine($"    {info.AccessModifiers} {info.Kind} {info.Name}{baseClause}");
                    writer.WriteLine("    {");

                    if (info.Kind == "enum")
                    {
                        foreach (var member in info.EnumMembers)
                        {
                            writer.WriteLine($"        {member},");
                        }
                    }
                    else
                    {
                        if (info.Fields.Count > 0)
                        {
                            writer.WriteLine("        // Fields");
                            foreach (var field in info.Fields) writer.WriteLine($"        {field}");
                            writer.WriteLine();
                        }

                        if (info.Properties.Count > 0)
                        {
                            writer.WriteLine("        // Properties");
                            foreach (var prop in info.Properties) writer.WriteLine($"        {prop}");
                            writer.WriteLine();
                        }

                        if (info.Events.Count > 0)
                        {
                            writer.WriteLine("        // Events");
                            foreach (var ev in info.Events) writer.WriteLine($"        {ev}");
                            writer.WriteLine();
                        }

                        if (info.Constructors.Count > 0)
                        {
                            writer.WriteLine("        // Constructors");
                            foreach (var ctor in info.Constructors) writer.WriteLine($"        {ctor}");
                            writer.WriteLine();
                        }

                        if (info.Methods.Count > 0)
                        {
                            writer.WriteLine("        // Methods");
                            foreach (var method in info.Methods) writer.WriteLine($"        {method}");
                        }
                    }

                    writer.WriteLine("    }");
                    writer.WriteLine("}");
                }
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in fileName)
            {
                if (invalidChars.Contains(c) || c == '<' || c == '>' || c == ':' || c == '*' || c == '?' || c == '"' || c == '|' || c == '/')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private void WriteCSharpSignatures(string filePath, string asmName, List<TypeSignatureInfo> infos)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine($"// ==========================================================================");
                writer.WriteLine($"// Assembly Signature Dump for: {asmName}");
                writer.WriteLine($"// Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"// Total Types: {infos.Count}");
                writer.WriteLine($"// ==========================================================================\n");

                var groups = infos.GroupBy(i => i.Namespace).OrderBy(g => g.Key);

                foreach (var group in groups)
                {
                    writer.WriteLine($"namespace {group.Key}");
                    writer.WriteLine("{");

                    foreach (var info in group)
                    {
                        string baseClause = "";
                        var bases = new List<string>();
                        if (!string.IsNullOrEmpty(info.BaseType)) bases.Add(info.BaseType);
                        if (info.Interfaces != null && info.Interfaces.Count > 0) bases.AddRange(info.Interfaces);
                        if (bases.Count > 0) baseClause = $" : {string.Join(", ", bases)}";

                        writer.WriteLine($"    {info.AccessModifiers} {info.Kind} {info.Name}{baseClause}");
                        writer.WriteLine("    {");

                        if (info.Kind == "enum")
                        {
                            foreach (var member in info.EnumMembers)
                            {
                                writer.WriteLine($"        {member},");
                            }
                        }
                        else
                        {
                            if (info.Fields.Count > 0)
                            {
                                writer.WriteLine("        // Fields");
                                foreach (var field in info.Fields) writer.WriteLine($"        {field}");
                                writer.WriteLine();
                            }

                            if (info.Properties.Count > 0)
                            {
                                writer.WriteLine("        // Properties");
                                foreach (var prop in info.Properties) writer.WriteLine($"        {prop}");
                                writer.WriteLine();
                            }

                            if (info.Events.Count > 0)
                            {
                                writer.WriteLine("        // Events");
                                foreach (var ev in info.Events) writer.WriteLine($"        {ev}");
                                writer.WriteLine();
                            }

                            if (info.Constructors.Count > 0)
                            {
                                writer.WriteLine("        // Constructors");
                                foreach (var ctor in info.Constructors) writer.WriteLine($"        {ctor}");
                                writer.WriteLine();
                            }

                            if (info.Methods.Count > 0)
                            {
                                writer.WriteLine("        // Methods");
                                foreach (var method in info.Methods) writer.WriteLine($"        {method}");
                            }
                        }

                        writer.WriteLine("    }\n");
                    }

                    writer.WriteLine("}\n");
                }
            }
        }

        private void WriteSummaryText(string filePath, string asmName, List<TypeSignatureInfo> infos)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                writer.WriteLine("==========================================================================");
                writer.WriteLine($"  ASSEMBLY SIGNATURE DUMP SUMMARY - {asmName}");
                writer.WriteLine("==========================================================================");
                writer.WriteLine($"Generated At    : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"Total Types     : {infos.Count}");
                writer.WriteLine($"Classes         : {infos.Count(i => i.Kind == "class")}");
                writer.WriteLine($"Structs         : {infos.Count(i => i.Kind == "struct")}");
                writer.WriteLine($"Enums           : {infos.Count(i => i.Kind == "enum")}");
                writer.WriteLine($"Interfaces      : {infos.Count(i => i.Kind == "interface")}");
                writer.WriteLine($"Delegates       : {infos.Count(i => i.Kind == "delegate")}");
                writer.WriteLine($"Total Methods   : {infos.Sum(i => i.Methods.Count)}");
                writer.WriteLine($"Total Fields    : {infos.Sum(i => i.Fields.Count)}");
                writer.WriteLine($"Total Properties: {infos.Sum(i => i.Properties.Count)}");
                writer.WriteLine("--------------------------------------------------------------------------\n");

                writer.WriteLine("NAMESPACES SUMMARY:");
                var namespaces = infos.GroupBy(i => i.Namespace).OrderByDescending(g => g.Count());
                foreach (var ns in namespaces)
                {
                    writer.WriteLine($"  - {ns.Key,-40} : {ns.Count()} types");
                }

                writer.WriteLine("\n--------------------------------------------------------------------------");
                writer.WriteLine("ALL DUMPED TYPE NAMES:");
                writer.WriteLine("--------------------------------------------------------------------------");
                foreach (var info in infos)
                {
                    writer.WriteLine($"[{info.Kind.ToUpper(),-9}] {info.FullName}");
                }
            }
        }

        private void WriteJsonMetadata(string filePath, List<TypeSignatureInfo> infos)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < infos.Count; i++)
            {
                var item = infos[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"Namespace\": \"{EscapeJson(item.Namespace)}\",");
                sb.AppendLine($"    \"Name\": \"{EscapeJson(item.Name)}\",");
                sb.AppendLine($"    \"FullName\": \"{EscapeJson(item.FullName)}\",");
                sb.AppendLine($"    \"Kind\": \"{EscapeJson(item.Kind)}\",");
                sb.AppendLine($"    \"AccessModifiers\": \"{EscapeJson(item.AccessModifiers)}\",");
                sb.AppendLine($"    \"BaseType\": {(item.BaseType == null ? "null" : $"\"{EscapeJson(item.BaseType)}\"")},");
                sb.AppendLine($"    \"Interfaces\": [{string.Join(", ", item.Interfaces.Select(x => $"\"{EscapeJson(x)}\""))}],");
                sb.AppendLine($"    \"EnumMembers\": [{string.Join(", ", item.EnumMembers.Select(x => $"\"{EscapeJson(x)}\""))}],");
                sb.AppendLine($"    \"FieldsCount\": {item.Fields.Count},");
                sb.AppendLine($"    \"PropertiesCount\": {item.Properties.Count},");
                sb.AppendLine($"    \"MethodsCount\": {item.Methods.Count}");
                sb.Append("  }");
                if (i < infos.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }
            sb.AppendLine("]");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
