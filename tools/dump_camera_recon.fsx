// Camera control recon: how MovieEditor maps keyboard/mouse to camera actions.
// Usage: dotnet fsi tools/dump_camera_recon.fsx > engine_host_spike/recon_camera.txt
open System.Reflection
open System.Reflection.Emit
open System.Reflection.PortableExecutable
open System.Reflection.Metadata
open System.Reflection.Metadata.Ecma335
open System.IO

let engineDir = @"C:\SeasunGame\MovieEditor\bin64"
let exePath = Path.Combine(engineDir, "MovieEditorHD.exe")

let esc (s: string) =
    let sb = System.Text.StringBuilder()
    for c in s.ToCharArray() do
        if int c > 126 then sb.Append(sprintf "\\u%04X" (int c)) |> ignore else sb.Append(c) |> ignore
    sb.ToString()

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

let resolveTok (md: MetadataReader) (tok: int) =
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

let opmap = System.Collections.Generic.Dictionary<int, OpCode>()
for f in typeof<OpCodes>.GetFields(BindingFlags.Public ||| BindingFlags.Static) do
    let op = f.GetValue(null) :?> OpCode
    if op.Size > 0 then opmap.[int op.Value] <- op

let dumpIL (path: string) (tn: string) (mn: string) =
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
                    while i < bytes.Length && guard < 60000 do
                        guard <- guard + 1
                        let b = int bytes.[i]
                        let key = if b = 0xFE && i + 1 < bytes.Length then 0xFE00 ||| int bytes.[i + 1] else b
                        let skip = if b = 0xFE then 2 else 1
                        match opmap.TryGetValue(key) with
                        | true, op ->
                            let ot = op.OperandType
                            let osz =
                                match ot with
                                | OperandType.InlineNone -> 0
                                | OperandType.ShortInlineI | OperandType.ShortInlineVar | OperandType.ShortInlineBrTarget -> 1
                                | OperandType.InlineVar -> 2
                                | OperandType.InlineI8 | OperandType.InlineR -> 8
                                | OperandType.InlineSwitch -> -1
                                | _ -> 4
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
                                    | _ -> resolveTok md (System.BitConverter.ToInt32(bytes, i + skip))
                                printfn "  IL_%04X: %s %s" i op.Name inlineText
                                i <- i + skip + osz
                        | false, _ -> i <- i + skip

let tokensOf (md: MetadataReader) (pe: PEReader) (m: MethodDefinition) =
    let res = System.Collections.Generic.List<int>()
    let mutable il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILReader()
    let bytes = il.ReadBytes(il.Length)
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
                let osz =
                    match ot.OperandType with
                    | OperandType.InlineNone -> 0
                    | OperandType.ShortInlineI | OperandType.ShortInlineVar | OperandType.ShortInlineBrTarget -> 1
                    | OperandType.InlineVar -> 2
                    | OperandType.InlineI8 | OperandType.InlineR -> 8
                    | OperandType.InlineSwitch -> -1
                    | _ -> 4
                if osz = -1 then
                    if i + skip + 4 <= bytes.Length then
                        let n2 = System.BitConverter.ToInt32(bytes, i + skip)
                        i <- i + skip + 4 + 4 * n2
                    else i <- bytes.Length
                elif osz = 4 && (ot.OperandType = OperandType.InlineMethod || ot.OperandType = OperandType.InlineField || ot.OperandType = OperandType.InlineType || ot.OperandType = OperandType.InlineString || ot.OperandType = OperandType.InlineTok) then
                    if i + skip + 4 <= bytes.Length then
                        res.Add(System.BitConverter.ToInt32(bytes, i + skip))
                        i <- i + skip + 4
                    else i <- bytes.Length
                else i <- i + skip + osz
            | false, _ -> i <- i + skip
    res

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
                    let toks = tokensOf md pe m
                    let hit = toks |> Seq.exists (fun tk -> let n = memberRefName md tk in names |> List.exists (fun x -> n.Contains(x)))
                    if hit then printfn "REF %s::%s" tn (md.GetString m.Name)
                with _ -> ()

let dumpType (path: string) (tn: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        if md.GetString(t.Name) = tn then
            printfn "-- %s.%s --" (md.GetString t.Namespace) tn
            for mh in t.GetMethods() do
                let m = md.GetMethodDefinition(mh)
                let isStatic = try m.Attributes.HasFlag(MethodAttributes.Static) with _ -> false
                printfn "   %s%s rva=%d" (if isStatic then "static " else "") (md.GetString m.Name) m.RelativeVirtualAddress
            for fh in t.GetFields() do
                let f = md.GetFieldDefinition(fh)
                printfn "   field %s" (md.GetString f.Name)

printfn "################ IL DUMPS ################"
dumpIL exePath "MainForm" "KeyBoardHookProc"
dumpIL exePath "MainForm" "SetKey_Hook"
dumpIL exePath "MainForm" "BeginHook"
dumpIL exePath "SceneForm" "SynchronizeCamera"
dumpIL exePath "SceneForm" "SetCameraVisible"
dumpIL exePath "ViewControlForm" "AddView"
dumpIL exePath "ViewControlForm" "ResetCameraView"
dumpIL exePath "ViewControlForm" "AddCameraView"
dumpIL exePath "ViewWindow" "ViewWindow_KeyDown"
dumpIL exePath "ViewWindow" "_OnKeyDown"
dumpIL exePath "ViewWindow" "_OnKey"
dumpIL exePath "ViewWindow" "ViewWindow_MouseDoubleClick"
dumpIL exePath "MiddleMapForm" "OnMiddleMapDoubleClick"
dumpIL exePath "CameraPosEditForm" "SetCurPosition"

printfn "################ REFS ################"
findRefs exePath [ "SetCamareMoveState"; "InputUnivrsalHotKey"; "SynchronizeCamera"; "SetCameraVisible" ]
findRefs exePath [ "GetCameraPos"; "SetCameraPos" ]
findRefs exePath [ "HotKeyTable"; "GetHotKey" ]

printfn "################ TYPES ################"
dumpType exePath "MainForm"
dumpType exePath "ViewWindow"
dumpType exePath "ViewControlForm"
dumpType exePath "CameraPosEditForm"

let listEnumsContaining (path: string) (needle: string) =
    use fs = File.OpenRead(path)
    use pe = new PEReader(fs)
    let md = pe.GetMetadataReader()
    for th in md.TypeDefinitions do
        let t = md.GetTypeDefinition(th)
        let n = md.GetString t.Name
        if n.ToUpperInvariant().Contains(needle) then
            printfn "ENUMCAND %s.%s" (md.GetString t.Namespace) n
            let fields = t.GetFields() |> Seq.map (fun fh -> md.GetFieldDefinition fh) |> Seq.filter (fun f -> f.Attributes.HasFlag(FieldAttributes.Literal))
            for f in fields do
                try
                    let c = md.GetConstant(f.GetDefaultValue())
                    let br = md.GetBlobReader(c.Value)
                    let v = if c.TypeCode = ConstantTypeCode.Int32 then br.ReadInt32() else 0
                    printfn "     %s = %d" (md.GetString f.Name) v
                with _ -> printfn "     %s = ?" (md.GetString f.Name)

listEnumsContaining exePath "CAM"
listEnumsContaining exePath "MOVE"
