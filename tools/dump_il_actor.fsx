// Scan MovieEditorHD for animated-model-on-map API usage + dump key methods.
// Usage: dotnet fsi tools/dump_il_actor.fsx
open System.Reflection
open System.Reflection.Emit
open System.Reflection.PortableExecutable
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open System.IO

let opmap = System.Collections.Generic.Dictionary<int, OpCode>()
for f in typeof<OpCodes>.GetFields(BindingFlags.Public ||| BindingFlags.Static) do
    let op = f.GetValue(null) :?> OpCode
    if op.Size > 0 then opmap.[int op.Value] <- op

let esc (s: string) =
    let sb = System.Text.StringBuilder()
    for c in s.ToCharArray() do
        if int c > 126 then sb.Append(sprintf "\\u%04X" (int c)) |> ignore else sb.Append(c) |> ignore
    sb.ToString()

let resolve (md: MetadataReader) (tok: int) =
    try
        match tok &&& 0xFF000000 with
        | 0x70000000 -> "str \"" + esc (md.GetUserString(MetadataTokens.UserStringHandle(tok &&& 0x00FFFFFF))) + "\""
        | 0x06000000 -> "meth " + md.GetString (md.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(tok &&& 0x00FFFFFF))).Name
        | 0x0A000000 ->
            let mr = md.GetMemberReference(MetadataTokens.MemberReferenceHandle(tok &&& 0x00FFFFFF))
            let pn =
                match mr.Parent.Kind with
                | HandleKind.TypeReference -> md.GetString (md.GetTypeReference(MetadataTokens.TypeReferenceHandle(MetadataTokens.GetToken(mr.Parent)))).Name
                | HandleKind.TypeDefinition -> md.GetString (md.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(MetadataTokens.GetToken(mr.Parent)))).Name
                | _ -> "?"
            "mref " + pn + "::" + (md.GetString mr.Name)
        | 0x04000000 -> "field " + md.GetString (md.GetFieldDefinition(MetadataTokens.FieldDefinitionHandle(tok &&& 0x00FFFFFF))).Name
        | 0x01000000 -> "type " + md.GetString (md.GetTypeReference(MetadataTokens.TypeReferenceHandle(tok &&& 0x00FFFFFF))).Name
        | 0x02000000 -> "typedef " + md.GetString (md.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(tok &&& 0x00FFFFFF))).Name
        | _ -> sprintf "tok%08X" tok
    with _ -> sprintf "tok%08X" tok

let operandSize (ot: OperandType) =
    match ot with
    | OperandType.InlineNone -> 0
    | OperandType.ShortInlineI | OperandType.ShortInlineVar | OperandType.ShortInlineBrTarget -> 1
    | OperandType.InlineVar -> 2
    | OperandType.InlineI8 | OperandType.InlineR -> 8
    | OperandType.InlineSwitch -> -1
    | _ -> 4

let dump (path: string) (tn: string) (mn: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        if md.GetString(t.Name) = tn then
            for mh in t.GetMethods() do
                let m = md.GetMethodDefinition(mh)
                if md.GetString(m.Name) = mn && m.RelativeVirtualAddress <> 0 then
                    printfn "===== %s::%s =====" tn mn
                    let mutable il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader()
                    let bytes = il.ReadBytes(il.Length)
                    let mutable i = 0
                    let mutable guard = 0
                    while i < bytes.Length && guard < 20000 do
                        guard <- guard + 1
                        let b = int bytes.[i]
                        let key = if b = 0xFE && i + 1 < bytes.Length then 0xFE00 ||| int bytes.[i + 1] else b
                        let skip = if b = 0xFE then 2 else 1
                        match opmap.TryGetValue(key) with
                        | true, op ->
                            let ot = op.OperandType
                            let osz = operandSize ot
                            if osz = -1 then
                                let n = System.BitConverter.ToInt32(bytes, i + skip)
                                printfn "  IL_%04X: %s (%d cases)" i op.Name n
                                i <- i + skip + 4 + 4 * n
                            elif osz = 0 then
                                printfn "  IL_%04X: %s" i op.Name
                                i <- i + skip
                            else
                                let inlineText =
                                    match ot with
                                    | OperandType.ShortInlineI -> string (sbyte bytes.[i + skip])
                                    | OperandType.InlineI -> string (System.BitConverter.ToInt32(bytes, i + skip))
                                    | OperandType.InlineI8 -> string (System.BitConverter.ToInt64(bytes, i + skip))
                                    | OperandType.InlineR -> string (System.BitConverter.ToSingle(bytes, i + skip))
                                    | OperandType.ShortInlineBrTarget -> sprintf "-> IL_%04X" (i + skip + 1 + int (sbyte bytes.[i + skip]))
                                    | OperandType.InlineBrTarget -> sprintf "-> IL_%04X" (i + skip + 4 + System.BitConverter.ToInt32(bytes, i + skip))
                                    | OperandType.ShortInlineVar -> string bytes.[i + skip]
                                    | OperandType.InlineVar -> string (System.BitConverter.ToUInt16(bytes, i + skip))
                                    | _ -> resolve md (System.BitConverter.ToInt32(bytes, i + skip))
                                printfn "  IL_%04X: %s %s" i op.Name inlineText
                                i <- i + skip + osz
                        | false, _ -> i <- i + skip

let scan (path: string) (needle: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        let tname = md.GetString t.Name
        for mh in t.GetMethods() do
            let m = md.GetMethodDefinition(mh)
            if m.RelativeVirtualAddress <> 0 then
                try
                    let il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader()
                    let bytes = il.ReadBytes(il.Length)
                    let mutable i = 0
                    let mutable guard = 0
                    let mutable running = true
                    while running && guard < 20000 do
                        guard <- guard + 1
                        if i < 0 || i >= bytes.Length then running <- false
                        else
                        let b = int bytes.[i]
                        let key = if b = 0xFE && i + 1 < bytes.Length then 0xFE00 ||| int bytes.[i + 1] else b
                        let skip = if b = 0xFE then 2 else 1
                        match opmap.TryGetValue(key) with
                        | true, op ->
                            let osz = operandSize op.OperandType
                            if osz = -1 then
                                if i + skip + 4 > bytes.Length then running <- false
                                else
                                    let n = System.BitConverter.ToInt32(bytes, i + skip)
                                    if n < 0 || i + skip + 4 + 4 * n > bytes.Length then running <- false
                                    else i <- i + skip + 4 + 4 * n
                            elif osz = 0 then i <- i + skip
                            else
                                if i + skip + osz > bytes.Length then running <- false
                                else
                                    if op.OperandType = OperandType.InlineMethod then
                                        let tok = System.BitConverter.ToInt32(bytes, i + skip)
                                        if (tok &&& 0xFF000000) = 0x0A000000 then
                                            try
                                                let mr = md.GetMemberReference(MetadataTokens.MemberReferenceHandle(tok &&& 0x00FFFFFF))
                                                let name = md.GetString mr.Name
                                                if name.Contains needle then
                                                    printfn "CALL %s::%s -> %s" tname (md.GetString m.Name) name
                                            with _ -> ()
                                    i <- i + skip + osz
                        | false, _ -> i <- i + skip
                with _ -> ()

let listMethods (path: string) (tn: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        if md.GetString(t.Name) = tn then
            printfn "===== methods %s =====" tn
            for mh in t.GetMethods() do
                let m = md.GetMethodDefinition(mh)
                if m.RelativeVirtualAddress <> 0 then
                    printfn "  %s" (md.GetString m.Name)

let scanCallsOn (path: string) (typeName: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        let tname = md.GetString t.Name
        for mh in t.GetMethods() do
            let m = md.GetMethodDefinition(mh)
            if m.RelativeVirtualAddress <> 0 then
                try
                    let il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader()
                    let bytes = il.ReadBytes(il.Length)
                    let mutable i = 0
                    let mutable guard = 0
                    let mutable running = true
                    while running && guard < 20000 do
                        guard <- guard + 1
                        if i < 0 || i >= bytes.Length then running <- false
                        else
                        let b = int bytes.[i]
                        let key = if b = 0xFE && i + 1 < bytes.Length then 0xFE00 ||| int bytes.[i + 1] else b
                        let skip = if b = 0xFE then 2 else 1
                        match opmap.TryGetValue(key) with
                        | true, op ->
                            let osz = operandSize op.OperandType
                            if osz = -1 then
                                if i + skip + 4 > bytes.Length then running <- false
                                else
                                    let n = System.BitConverter.ToInt32(bytes, i + skip)
                                    if n < 0 || i + skip + 4 + 4 * n > bytes.Length then running <- false
                                    else i <- i + skip + 4 + 4 * n
                            elif osz = 0 then i <- i + skip
                            else
                                if i + skip + osz > bytes.Length then running <- false
                                else
                                    if op.OperandType = OperandType.InlineMethod then
                                        let tok = System.BitConverter.ToInt32(bytes, i + skip)
                                        if (tok &&& 0xFF000000) = 0x0A000000 then
                                            try
                                                let mr = md.GetMemberReference(MetadataTokens.MemberReferenceHandle(tok &&& 0x00FFFFFF))
                                                let pn =
                                                    match mr.Parent.Kind with
                                                    | HandleKind.TypeReference -> md.GetString (md.GetTypeReference(MetadataTokens.TypeReferenceHandle(MetadataTokens.GetToken(mr.Parent)))).Name
                                                    | HandleKind.TypeDefinition -> md.GetString (md.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(MetadataTokens.GetToken(mr.Parent)))).Name
                                                    | _ -> "?"
                                                if pn = typeName then
                                                    printfn "CALL %s::%s -> %s::%s" tname (md.GetString m.Name) pn (md.GetString mr.Name)
                                            with _ -> ()
                                    i <- i + skip + osz
                        | false, _ -> i <- i + skip
                with _ -> ()

let exePath = @"C:\SeasunGame\MovieEditor\bin64\MovieEditorHD.exe"
let clrPath = @"C:\SeasunGame\MovieEditor\bin64\MovieEngineCLR.dll"

let listEnum (path: string) (needle: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        let tn = md.GetString(t.Name)
        if tn.Contains needle then
            let isEnum = (t.BaseType.Kind = HandleKind.TypeReference) &&
                         (md.GetString (md.GetTypeReference(MetadataTokens.TypeReferenceHandle(MetadataTokens.GetToken(t.BaseType)))).Name = "Enum")
            if isEnum then
                printfn "===== enum %s =====" tn
                for fh in t.GetFields() do
                    let f = md.GetFieldDefinition(fh)
                    let name = md.GetString f.Name
                    if name <> "value__" then
                        let cv = f.GetDefaultValue()
                        if not cv.IsNil then
                            let c = md.GetConstant(cv)
                            let br = md.GetBlobReader(c.Value)
                            printfn "  %s = %d" name (br.ReadInt32())
                        else printfn "  %s = ?" name

printfn "########## dump ActorEditorCommandHelper::SetObjectProperty"
dump exePath "ActorEditorCommandHelper" "SetObjectProperty"
printfn "########## dump ActorEditorCommandHelper::GetObjectProperty"
dump exePath "ActorEditorCommandHelper" "GetObjectProperty"
printfn "########## methods KGGizmoAxisOperator_CLR"
listMethods clrPath "KGGizmoAxisOperator_CLR"
printfn "########## methods KG_TransformCLR (if any)"
listMethods clrPath "KG_TransformCLR"
printfn "########## enums EPT_Object_Trans value context"
scan exePath "OnAction"
