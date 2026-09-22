# Recon — MovieEditor camera control system (IL-verified)

Dumps: `tools/dump_camera_recon.fsx` -> `engine_host_spike/recon_camera.txt` +
`recon_il.txt` (ViewWindow handlers). This documents the EXACT input->action
mapping the editor uses, so the host can adopt the same system instead of a
homegrown one.

## Core API (KGSceneCLR)

```
ExecAction(int action, int arg2, int arg3, int lParam)   // (EXEACTION, state, 0, MakeLParam(x,y))
SetCamareMoveState(int flags, int state)                 // WASD-style camera move machine (void)
GetCameraPos(float&, float&, float&) / SetCameraPos(x,y,z,bool)
ResetCameraPosLookAtUp() / ResetCameraPosLookAtUpFromSky()
InputUnivrsalHotKey()                                    // engine-side hotkey processor
FocusOnModel()
AddOutputWindow(name, hwnd, OUTPUTWND)                   // SCENE_MAIN=0, OBJECT_PREVEIW=2
```

`EXEACTION` (full enum, from MovieEditorHD metadata):

| value | name | used by |
|---|---|---|
| 1 | ROTATE_CAMERA | Alt+Right-drag (orbit) |
| 2 | ZOOM_VIEW | (drag zoom; runaway in host, avoid) |
| 3 | PAN_VIEW | Right-drag |
| 4 | ROTATE_VIEW | Shift+Right-drag |
| 5 | INVERT_ACTION | — |
| 6-15 | GO_FORWARD..TURN_RIGHT_VIEW_FOLLOWED | camera walk keys (legacy action set) |
| 16 | PRESURE | — |
| 17/18 | INNER_CIRCLE / OUTTER_CIRCLE | — |
| 19 | LEFE_KEY_DOWN | left button down/up marker |
| 20/21/22 | SELECTION_ADD / SUTRACT / NOGROUP | Ctrl/Alt/Shift + Left-drag |
| 23 | POWER | — |
| 24 | SMOOTH_TERRAIN | — |
| 25/26 | SPEED_UP / SPEED_DOWN | numpad + / - |
| 27/28 | MOV_UP / MOV_DOWN | — |
| 29 | SET_EDITSTATE | — |
| 30 | MOUSE_MOVE | sent on EVERY mouse move (input reference) |
| 31 | MOUSE_WHEEL | wheel zoom (arg3=direction, arg4=1) |
| 32/33 | GETHEIGHT/SETHEIGHT_TERRAIN | terrain tools |
| 50-53 | HOME/PAGEUP/PAGEDOWN/DELETE | — |
| 1001 | ZOOM_TO_OBJECT | hotkey (default id 4) |
| 1002 | PLAY_CAM_ANI | — |
| 1004 | LOCATE_TO_OBJECT | Alt+F |
| 2004 | DE_MOVE_ANEAR_GROUND | — |
| 2100-2103 | SET_STATE_SELECT/MOVE/ROTATE/SCALE | edit states |
| 3000 | PLAY_PLOT | — |

`CAMERA_MOVE_STATE` (flags for SetCamareMoveState):

| flag | name | trigger |
|---|---|---|
| 1 | cmsFoward | W |
| 2 | cmsBack | S |
| 4 | cmsTurnLeft | (unbound in ViewWindow::_OnKey) |
| 64 | cmsMoveLeft | A |
| 128 | cmsMoveRight | D |
| 256 | cmsCamareUp | hotkey id 11 (configurable) |
| 512/1024 | cmsSpeedUp/Down | (unbound) |
| 2048 | cmsCamareDown | hotkey id 12 (configurable) |
| 4096 | cmsFastMove | Shift held |

## Mouse mapping (ViewWindow::ViewWindow_MouseDown/Move/Up/OnMouseWheel)

lParam = MakeLParam(x,y) = ((y & 0xFFFF) << 16) | (x & 0xFFFF).

- **MouseDown**
  - Left: Ctrl -> ExecAction(20,1,0,lp); Alt -> (21,1,0,lp); Shift -> (22,1,0,lp);
    always -> (19,1,0,lp). (Selection variants.)
  - Right: nothing.
- **MouseMove** (every move):
  1. ALWAYS first: ExecAction(30,1,0,lp) — MOUSE_MOVE input reference.
  2. Right, no modifiers -> (3,1,0,lp) PAN_VIEW
     Right+Shift -> (4,1,0,lp) ROTATE_VIEW
     Right+Alt -> (1,1,0,lp) ROTATE_CAMERA
  3. Left -> (19,1,0,lp)
- **MouseUp**
  - Left -> (19,0,0,lp)
  - Right, no Ctrl -> ShowContextMenu; Right+Ctrl -> ChangeMovieEditorState (exit
    edit state). No explicit drag-release action for right-drag.
- **MouseWheel**
  - Ctrl held -> viewport resolution ratio change (not camera).
  - Else: delta<0 -> ExecAction(31,1,0,1); delta>=0 -> (31,1,1,1).
- **MouseDoubleClick**: Right -> ChangeMovieEditorState(null,0,1) (exit edit state).

So in the real editor the ORBIT is **Alt+Right-drag**, PAN is **plain Right-drag**,
ROTATE_VIEW is **Shift+Right-drag**; LEFT drag is selection, not camera.
Action 30 on every move is what fixes drag-start jumps (the input reference).

## Keyboard mapping (ViewWindow::_OnKeyDown / _OnKey, keyCode)

Only handled when no Ctrl/Alt/Shift is held (except explicit Alt combos below).

| key | call |
|---|---|
| W (87) | SetCamareMoveState(1 | shift?4096:0, state) |
| S (83) | SetCamareMoveState(2 | shift?4096:0, state) |
| A (65) | SetCamareMoveState(64 | shift?4096:0, state) |
| D (68) | SetCamareMoveState(128 | shift?4096:0, state) |
| Shift (16) | SetCamareMoveState(4096, state) |
| NumpadAdd (107) | ExecAction(25, state, 0, 0) SPEED_UP |
| NumpadSubtract (109) | ExecAction(26, state, 0, 0) SPEED_DOWN |
| Escape (27) | UpdateAllView(...) (refresh) |
| Alt+X (88) | UpdateAllView(...) |
| Alt+F (70) | ExecAction(1004,0,0,0) LOCATE_TO_OBJECT |

`state` = 1 on KeyDown, 0 on KeyUp (via ViewWindow_KeyDown/_OnKeyDown).

## Global hotkeys (MainForm::KeyBoardHookProc, low-level hook)

Configurable (MovieEditorConfig.xml `EnableHotKey=True`; table via
GetHotKey(ctrl,alt,shift,vk) + HotKeyTable). Switch ids (0-based after -1):
1 thread update, 2 Undo, 3 Redo, 4 ExecAction(1001=ZOOM_TO_OBJECT),
5/6 editor-state / OpenMiddleMap, 7 MoveSelectedAction, 8 ShowCommandPanel,
9/10 gizmo size, 11 SetCamareMoveState(256|shift4096, 1) camera UP,
12 SetCamareMoveState(2048|shift4096, 1) camera DOWN, 13 ChangeCoordMode,
14-17 keyframes/frames, 18 JumpPlot. Hook also calls
`scene.InputUnivrsalHotKey()` on every key event.

## Camera infrastructure found

- `ViewControlForm`: AddView (creates docked ViewWindow + SceneForm.AddOutputWindow
  with flag **0 = SCENE_MAIN**), ResetCameraView, AddCameraView ("摄像机视图" —
  a view bound to a camera element in the scene), FrameMove.
- `ViewWindow`: owns per-window OutputWindowID + the handlers above.
- `SceneForm::MoveCamera(int flags, int state)` -> `KGSceneCLR.SetCamareMoveState`.
- `SceneForm::SynchronizeCamera` — copy camera transform from selected camera
  element to the view (camera elements as view sources).
- `MiddleMapForm::OnMiddleMapDoubleClick` — minimap double-click -> camera jump
  (PictureBoxPointToImgPoint -> MiddleMapData).
- `CameraPosEditForm` — read-only x,y,z display from GetCameraPos.
- `Camera.CameraBookMarkForm` — camera bookmarks.

## Adoption notes for the host

1. Use OUTPUTWND flag 0 (SCENE_MAIN) for the main view, not 2 (OBJECT_PREVEIW).
2. Right-drag = pan (3), Alt+Right = orbit (1), Shift+Right = rotate (4);
   left = selection refs (19/30). Send 30 before every drag action on move.
3. WASD/Shift/Q/E via SetCamareMoveState flags (editor hotkeys 11/12 = up/down;
   host binds Q/E to those). Numpad +/- -> 25/26.
4. Engine moves the camera itself each FrameMove while move-state bits are set.
5. ROTATE_VIEW(4)/PAN_VIEW(3) were no-ops in the actor spike host — verify in the
   map scene (MAP_MOVETEST). If no-ops there too, they depend on editor
   selection/edit-state context.
