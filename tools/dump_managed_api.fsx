// Dump managed API signatures from MovieEngineCLR.dll / MovieEditorHD.exe.
// Usage: dotnet fsi tools/dump_managed_api.fsx > out.txt
open System.Reflection
open System.Reflection.Emit
open System.Reflection.PortableExecutable
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open System.IO

let engineDir = @"C:\SeasunGame\MovieEditor\bin64"
let clrPath = Path.Combine(engineDir, "MovieEngineCLR.dll")
let exePath = Path.Combine(engineDir, "MovieEditorHD.exe")

let typeNameOfDefOrRef (md: MetadataReader) (coded: int) =
    try
        let kind = coded &&& 3
        let idx = coded >>> 2
        match kind with
        | 0 ->
            let t = md.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(idx))
            let ns = md.GetString t.Namespace
            let n = md.GetString t.Name
            if ns = "" then n else ns + "." + n
        | 1 ->
            let t = md.GetTypeReference(MetadataTokens.TypeReferenceHandle(idx))
            let ns = md.GetString t.Namespace
            let n = md.GetString t.Name
            if ns = "" then n else ns + "." + n
        | 2 -> "typespec"
        | _ -> sprintf "coded%X" coded
    with _ -> sprintf "coded%X" coded

type BlobCursor(bytes: byte[]) =
    let mutable pos = 0
    member _.ReadByte() =
        let b = bytes.[pos]
        pos <- pos + 1
        b
    member _.ReadCompressedInteger() =
        let b0 = int (bytes.[pos])
        pos <- pos + 1
        if b0 &&& 0x80 = 0 then b0
        elif b0 &&& 0xC0 = 0x80 then
            let b1 = int (bytes.[pos])
            pos <- pos + 1
            ((b0 &&& 0x3F) <<< 8) ||| b1
        else
            let b1 = int (bytes.[pos])
            let b2 = int (bytes.[pos + 1])
            let b3 = int (bytes.[pos + 2])
            pos <- pos + 3
            ((b0 &&& 0x1F) <<< 24) ||| (b1 <<< 16) ||| (b2 <<< 8) ||| b3

let rec decodeType (md: MetadataReader) (r: BlobCursor) : string =
    let et = r.ReadByte()
    match et with
    | 0x01uy -> "void"
    | 0x02uy -> "bool"
    | 0x03uy -> "char"
    | 0x04uy -> "sbyte"
    | 0x05uy -> "byte"
    | 0x06uy -> "short"
    | 0x07uy -> "ushort"
    | 0x08uy -> "int"
    | 0x09uy -> "uint"
    | 0x0Auy -> "long"
    | 0x0Buy -> "ulong"
    | 0x0Cuy -> "float"
    | 0x0Duy -> "double"
    | 0x0Euy -> "string"
    | 0x0Fuy -> (decodeType md r) + "*"
    | 0x10uy -> (decodeType md r) + "&"
    | 0x11uy
    | 0x12uy ->
        let c = r.ReadCompressedInteger()
        typeNameOfDefOrRef md c
    | 0x13uy ->
        let i = r.ReadCompressedInteger()
        sprintf "!%d" i
    | 0x14uy ->
        let t = decodeType md r
        let rank = r.ReadCompressedInteger()
        t + "[" + String.replicate (max 1 rank) "," + "]"
    | 0x15uy ->
        let t = decodeType md r
        let argc = r.ReadCompressedInteger()
        let args = [ for _ in 1 .. argc -> decodeType md r ]
        t + "<" + String.concat "," args + ">"
    | 0x18uy -> "IntPtr"
    | 0x19uy -> "UIntPtr"
    | 0x1Buy -> "fnptr"
    | 0x1Cuy -> "object"
    | 0x1Duy -> (decodeType md r) + "[]"
    | 0x1Euy ->
        let i = r.ReadCompressedInteger()
        sprintf "!!%d" i
    | 0x1Fuy
    | 0x20uy ->
        let coded = r.ReadCompressedInteger()
        let modName = typeNameOfDefOrRef md coded
        let inner = decodeType md r
        inner + " mod" + (if et = 0x1Fuy then "req" else "opt") + "(" + modName + ")"
    | _ -> sprintf "et%02X" et

let methodSig (md: MetadataReader) (m: MethodDefinition) : string =
    try
        let br = BlobCursor(md.GetBlobBytes(m.Signature))
        br.ReadByte() |> ignore // calling convention
        let nparams = br.ReadCompressedInteger()
        let ret = decodeType md br
        let pars = [ for _ in 1 .. nparams -> decodeType md br ]
        ret + " (" + String.concat ", " pars + ")"
    with e -> "sig-error: " + e.Message

let dumpAssembly (path: string) (targets: string list) (listAll: bool) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    printfn "==== %s ====" (Path.GetFileName path)
    printfn "metadataVersion=%s" md.MetadataVersion
    if pe.HasMetadata && pe.PEHeaders.CorHeader <> null then
        printfn "corFlags=%A" pe.PEHeaders.CorHeader.Flags
    printfn "-- assembly refs --"
    for ah in md.AssemblyReferences do
        let a = md.GetAssemblyReference(ah)
        printfn "   %s %A" (md.GetString a.Name) a.Version
    if listAll then
        printfn "-- types (filtered) --"
        for th in md.TypeDefinitions do
            let t = md.GetTypeDefinition(th)
            let ns = md.GetString t.Namespace
            let n = md.GetString t.Name
            if n.Contains("KG") || n.Contains("Movie") || n.Contains("Ani") || n.Contains("Actor") || n.Contains("Scene") then
                printfn "   %s%s" (if ns = "" then "" else ns + ".") n
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        let n = md.GetString t.Name
        if List.contains n targets then
            printfn "-- %s.%s --" (md.GetString t.Namespace) n
            for mh in t.GetMethods() do
                let m = md.GetMethodDefinition(mh)
                let isStatic =
                    try m.Attributes.HasFlag(MethodAttributes.Static) with _ -> false
                printfn "   %s%s %s" (if isStatic then "static " else "") (md.GetString m.Name) (methodSig md m)
            for fh in t.GetFields() do
                let f = md.GetFieldDefinition(fh)
                printfn "   field %s" (md.GetString f.Name)

// IL walker for finding the engine-init method in MovieEditorHD.exe
let opmap = System.Collections.Generic.Dictionary<int, OperandType>()
for f in typeof<OpCodes>.GetFields(BindingFlags.Public ||| BindingFlags.Static) do
    let op = f.GetValue(null) :?> OpCode
    if op.Size > 0 then opmap.[int op.Value] <- op.OperandType
let oszOf (ot: OperandType) =
    match ot with
    | OperandType.InlineNone -> 0
    | OperandType.ShortInlineI
    | OperandType.ShortInlineVar
    | OperandType.ShortInlineBrTarget -> 1
    | OperandType.InlineVar -> 2
    | OperandType.InlineI8
    | OperandType.InlineR -> 8
    | OperandType.InlineSwitch -> -1
    | _ -> 4
let tokensOf (bytes: byte[]) =
    let res = System.Collections.Generic.List<int>()
    let mutable i = 0
    let mutable guard = 0
    while i < bytes.Length && guard < 100000 do
        guard <- guard + 1
        let b = int bytes.[i]
        let key = if b = 0xFE && i + 1 < bytes.Length then 0xFE00 ||| int bytes.[i + 1] else b
        let skip = if b = 0xFE then 2 else 1
        if i + skip > bytes.Length then i <- bytes.Length
        else
            match opmap.TryGetValue(key) with
            | true, ot ->
                let osz = oszOf ot
                if osz = -1 then
                    if i + skip + 4 <= bytes.Length then
                        let n2 = System.BitConverter.ToInt32(bytes, i + skip)
                        i <- i + skip + 4 + 4 * n2
                    else i <- bytes.Length
                elif
                    osz = 4
                    && (ot = OperandType.InlineMethod
                        || ot = OperandType.InlineField
                        || ot = OperandType.InlineType
                        || ot = OperandType.InlineString
                        || ot = OperandType.InlineTok)
                then
                    if i + skip + 4 <= bytes.Length then
                        res.Add(System.BitConverter.ToInt32(bytes, i + skip))
                        i <- i + skip + 4
                    else i <- bytes.Length
                else i <- i + skip + osz
            | false, _ -> i <- i + skip
    res

let memberRefName (md: MetadataReader) (tok: int) =
    try
        match tok &&& 0xFF000000 with
        | 0x0A000000 ->
            let mr = md.GetMemberReference(MetadataTokens.MemberReferenceHandle(tok &&& 0x00FFFFFF))
            let pn =
                match mr.Parent.Kind with
                | HandleKind.TypeReference ->
                    md.GetString (md.GetTypeReference(MetadataTokens.TypeReferenceHandle(MetadataTokens.GetToken(mr.Parent)))).Name
                | HandleKind.TypeDefinition ->
                    md.GetString (md.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(MetadataTokens.GetToken(mr.Parent)))).Name
                | _ -> "?"
            pn + "::" + (md.GetString mr.Name)
        | _ -> ""
    with _ -> ""

let findEngineInit (path: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    printfn "==== engine init refs in %s ====" (Path.GetFileName path)
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        let tn = (md.GetString t.Namespace) + "." + (md.GetString t.Name)
        for mh in t.GetMethods() do
            let m = md.GetMethodDefinition(mh)
            if m.RelativeVirtualAddress <> 0 then
                try
                    let mutable il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader()
                    let bytes = il.ReadBytes(il.Length)
                    let toks = tokensOf bytes
                    let hit =
                        toks
                        |> Seq.exists (fun tk ->
                            let n = memberRefName md tk
                            n.Contains("KGEngineCLR") || n.Contains("KMovieCore") || n.Contains("NewEmptyScene"))
                    if hit then
                        printfn "  %s::%s" tn (md.GetString m.Name)
                with _ -> ()

dumpAssembly clrPath
    [ "KGEngineCLR"; "KMovieCore"; "KGSceneCLR"; "KGMovieActorCLR"; "KGModelCLR" ]
    true
findEngineInit exePath

let esc (s:string) =
    let sb = System.Text.StringBuilder()
    for c in s.ToCharArray() do
        if int c > 126 then sb.Append(sprintf "\\u%04X" (int c)) |> ignore else sb.Append(c) |> ignore
    sb.ToString()

let resolveMemberStr (md: MetadataReader) (tok: int) =
    try
        match tok &&& 0xFF000000 with
        | 0x70000000 -> "str \"" + esc (md.GetUserString(MetadataTokens.UserStringHandle(tok &&& 0x00FFFFFF))) + "\""
        | 0x06000000 -> "meth " + md.GetString (md.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(tok &&& 0x00FFFFFF))).Name
        | 0x0A000000 -> "mref " + memberRefName md tok
        | 0x04000000 -> "field " + md.GetString (md.GetFieldDefinition(MetadataTokens.FieldDefinitionHandle(tok &&& 0x00FFFFFF))).Name
        | 0x01000000 -> "type " + md.GetString (md.GetTypeReference(MetadataTokens.TypeReferenceHandle(tok &&& 0x00FFFFFF))).Name
        | 0x02000000 -> "typedef " + md.GetString (md.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(tok &&& 0x00FFFFFF))).Name
        | _ -> sprintf "tok%08X" tok
    with _ -> sprintf "tok%08X" tok

let dumpMethodIL (path: string) (tn: string) (mn: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        if md.GetString(t.Name) = tn then
            for mh in t.GetMethods() do
                let m = md.GetMethodDefinition(mh)
                if md.GetString(m.Name) = mn && m.RelativeVirtualAddress <> 0 then
                    printfn "===== %s::%s IL =====" tn mn
                    let mutable il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader()
                    let bytes = il.ReadBytes(il.Length)
                    let mutable i = 0
                    let mutable guard = 0
                    while i < bytes.Length && guard < 20000 do
                        guard <- guard + 1
                        let b = int bytes.[i]
                        let key = if b = 0xFE && i + 1 < bytes.Length then 0xFE00 ||| int bytes.[i + 1] else b
                        let skip = if b = 0xFE then 2 else 1
                        if i + skip > bytes.Length then i <- bytes.Length
                        else
                            match opmap.TryGetValue(key) with
                            | true, ot ->
                                let osz = oszOf ot
                                if osz = -1 then
                                    if i + skip + 4 <= bytes.Length then
                                        let n2 = System.BitConverter.ToInt32(bytes, i + skip)
                                        i <- i + skip + 4 + 4 * n2
                                    else i <- bytes.Length
                                elif
                                    osz = 4
                                    && (ot = OperandType.InlineMethod
                                        || ot = OperandType.InlineField
                                        || ot = OperandType.InlineType
                                        || ot = OperandType.InlineString
                                        || ot = OperandType.InlineTok)
                                then
                                    if i + skip + 4 <= bytes.Length then
                                        let tok = System.BitConverter.ToInt32(bytes, i + skip)
                                        printfn "  IL_%04X: %s" i (resolveMemberStr md tok)
                                        i <- i + skip + 4
                                    else i <- bytes.Length
                                else i <- i + skip + osz
                            | false, _ -> i <- i + skip

dumpMethodIL exePath "EngineLayer" "Init"
dumpMethodIL exePath "EngineLayer" "InitBaseLib"
dumpMethodIL exePath "EngineLayer" "NewScene"
dumpAssembly exePath ["EngineLayer"; "Config"] false
dumpAssembly clrPath ["KGBaseCLR"; "KGMovieEditorCLR"; "KG3DSoundCLR"] false
dumpMethodIL exePath "EngineLayer" "get_EngineDir"
dumpMethodIL exePath "EngineLayer" "get_WorkingDir"
let findMethodOwners (path: string) (names: string list) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        for mh in t.GetMethods() do
            let m = md.GetMethodDefinition(mh)
            let n = md.GetString(m.Name)
            if List.contains n names then
                printfn "OWNER %s.%s::%s rva=%d" (md.GetString t.Namespace) (md.GetString t.Name) n m.RelativeVirtualAddress

findMethodOwners exePath ["get_EngineDir"; "get_WorkingDir"; "get_EnableHttpFile"; "get_HttpConfig"; "get_ActorCreateOption"; "get_RtxRadiusMode"; "get_EnableConsole"; "get_EnableModelAsyncLoad"; "get_EnableMapAysncLoad"]
dumpMethodIL exePath "Config" "get_EngineDir"
dumpMethodIL exePath "Config" "get_WorkingDir"
dumpMethodIL exePath "Config" "get_EnableHttpFile"
dumpMethodIL exePath "Config" "get_HttpConfig"
dumpMethodIL exePath "EditorLayer" "get_EngineDir"
dumpMethodIL exePath "EditorLayer" "get_WorkingDir"
dumpMethodIL exePath "EditorConfig" "get_EnableHttpFile"
dumpMethodIL exePath "EditorConfig" "get_HttpConfig"
dumpMethodIL exePath "EditorConfig" "get_ActorCreateOption"
dumpAssembly exePath ["EditorLayer"; "EditorConfig"] false
dumpMethodIL exePath "EditorLayer" ".ctor"
dumpMethodIL exePath "EditorLayer" "Init"
dumpAssembly exePath ["ActorEditorCommandHelper"] false
dumpMethodIL exePath "ActorEditorCommandHelper" "LoadFromFile"
dumpMethodIL exePath "ActorEditorCommandHelper" "SetObjectProperty"
dumpMethodIL exePath "ActorEditorCommandHelper" "GetObjectProperty"
dumpMethodIL clrPath "KGMovieActorCLR" "Init"
dumpMethodIL exePath "EditorLayer" "SetWorkingDir"
dumpMethodIL exePath "EditorLayer" "SetEngineDir"
dumpMethodIL exePath "EditorLayer" "ReadMovieEditorINI"
dumpMethodIL exePath "EditorLayer" "Init3DEngine"
dumpMethodIL exePath "KPlayerListForm" "buttonItem_load_Click"
dumpMethodIL exePath "KPlayerCheckTool" "InitPlayerDressForm"
findMethodOwners exePath ["get_MainFrmHwnd"]
let findRefs (path: string) (names: string list) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        let tn = (md.GetString t.Namespace) + "." + (md.GetString t.Name)
        for mh in t.GetMethods() do
            let m = md.GetMethodDefinition(mh)
            if m.RelativeVirtualAddress <> 0 then
                try
                    let mutable il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader()
                    let bytes = il.ReadBytes(il.Length)
                    let toks = tokensOf bytes
                    let hit = toks |> Seq.exists (fun tk -> let n = memberRefName md tk in names |> List.exists (fun x -> n.Contains(x)))
                    if hit then printfn "REF %s::%s" tn (md.GetString m.Name)
                with _ -> ()
findRefs exePath ["AddOutputWindow"; "SetPrimaryWindow"; "RemoveOutputWindow"; "RecreateOutputWindow"; "SetCurOutputWindow"]
dumpAssembly exePath ["ViewWindow"] false
dumpAssembly exePath ["SceneForm"] false
findRefs exePath ["SetScreenShot"; "DoScreenShotImmediate"]
findRefs exePath ["SetCamareMoveState"; "InputUnivrsalHotKey"; "MoveCamera"]
let dumpEnum (path: string) (name: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        if md.GetString(t.Name) = name then
            printfn "-- enum %s --" name
            for fh in t.GetFields() do
                let f = md.GetFieldDefinition(fh)
                if f.Attributes.HasFlag(FieldAttributes.Literal) then
                    try
                        let c = md.GetConstant(f.GetDefaultValue())
                        let br = md.GetBlobReader(c.Value)
                        let v = if c.TypeCode = ConstantTypeCode.Int32 then br.ReadInt32() else 0
                        printfn "   %s = %d" (md.GetString f.Name) v
                    with _ -> printfn "   %s = ?" (md.GetString f.Name)
dumpEnum exePath "EXEACTION"
dumpEnum exePath "OUTPUTWND"
findRefs exePath ["UpdateSoundShell"; "atlSound"; "KG3DSoundCLR"]
findRefs exePath ["SetCameraPos"; "GetCameraPos"; "SetCamareMoveState"]
