extends SceneTree
## Dev-only visual verification harness.
##
## Renders a scene offscreen (under Xvfb) and writes a PNG so UI changes can be
## verified with real pixels instead of assumptions.
##
## Usage:
##   godot-mono --path . --script res://tools/ui_capture.gd -- \
##       --scene res://scenes/ui/MainMenu.tscn --out /tmp/shot.png --wait 40
##
## Options:
##   --scene <path>      scene to instantiate (required)
##   --out <path>        PNG destination (required)
##   --wait <n>          frame to capture on (default 30)
##   --act <n>           frame to run interactions on (default: wait - 15)
##   --visible           force the root + child visible with alpha 1
##   --child <path>      instantiate a second scene as a child of the root
##   --invoke <method>   call a method on the root (e.g. Open)
##   --press <path>      emit `pressed` on a Button at <path> (real user flow)
##   --focus <path>      grab focus on a Control at <path>
##   --hover <path>      emit `button_entered` on a Button at <path>

var _scene_path := ""
var _out_path := ""
var _wait_frames := 30
var _act_frame := -1
var _invoke := ""
var _child_scene := ""
var _press_path := ""
var _focus_path := ""
var _hover_path := ""
var _force_visible := false
var _frames := 0
var _acted := false
var _captured := false
var _root_node: Node = null
var _child_node: Node = null


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	var i := 0
	while i < args.size():
		match args[i]:
			"--scene":
				_scene_path = args[i + 1]
				i += 1
			"--out":
				_out_path = args[i + 1]
				i += 1
			"--wait":
				_wait_frames = int(args[i + 1])
				i += 1
			"--act":
				_act_frame = int(args[i + 1])
				i += 1
			"--visible":
				_force_visible = true
			"--child":
				_child_scene = args[i + 1]
				i += 1
			"--invoke":
				_invoke = args[i + 1]
				i += 1
			"--press":
				_press_path = args[i + 1]
				i += 1
			"--focus":
				_focus_path = args[i + 1]
				i += 1
			"--hover":
				_hover_path = args[i + 1]
				i += 1
		i += 1

	if _act_frame < 0:
		_act_frame = maxi(_wait_frames - 15, 1)

	if _scene_path == "" or _out_path == "":
		printerr("ui_capture: --scene and --out are required")
		quit(1)
		return

	var packed := load(_scene_path) as PackedScene
	if packed == null:
		printerr("ui_capture: cannot load %s" % _scene_path)
		quit(1)
		return

	var node := packed.instantiate()
	root.add_child(node)
	_root_node = node

	if _child_scene != "":
		var child_packed := load(_child_scene) as PackedScene
		if child_packed != null:
			var child := child_packed.instantiate()
			node.add_child(child)
			_child_node = child

	RenderingServer.frame_post_draw.connect(_on_post_draw)


func _act() -> void:
	# Interactions run on a later frame so the target's _Ready has completed
	# (Godot defers _Ready for nodes added during tree initialisation, and an
	# overlay that is opened in the same frame never becomes visible).
	if _force_visible:
		_show(_root_node)
		if _child_node != null:
			_show(_child_node)

	if _invoke != "" and _root_node.has_method(_invoke):
		_root_node.call(_invoke)
	if _child_node != null and _child_node.has_method("Open"):
		_child_node.call("Open")

	if _press_path != "":
		var p := _root_node.get_node_or_null(NodePath(_press_path))
		if p != null and p is BaseButton:
			(p as BaseButton).pressed.emit()
	if _hover_path != "":
		var h := _root_node.get_node_or_null(NodePath(_hover_path))
		if h != null and h is BaseButton:
			(h as BaseButton).button_entered.emit()
	if _focus_path != "":
		var f := _root_node.get_node_or_null(NodePath(_focus_path))
		if f != null and f is Control:
			(f as Control).grab_focus()


func _show(n: Node) -> void:
	if n is CanvasLayer:
		(n as CanvasLayer).visible = true
		var ml: Color = (n as CanvasLayer).modulate
		ml.a = 1.0
		(n as CanvasLayer).modulate = ml
	if n is CanvasItem:
		(n as CanvasItem).visible = true
		var mi: Color = (n as CanvasItem).modulate
		mi.a = 1.0
		(n as CanvasItem).modulate = mi
	for c in n.get_children():
		_show(c)


func _on_post_draw() -> void:
	_frames += 1
	if not _acted and _frames >= _act_frame:
		_acted = true
		_act()
		return
	if _captured or _frames < _wait_frames:
		return
	_captured = true

	await RenderingServer.frame_post_draw

	_dump_state("root", _root_node)
	if _child_node != null:
		_dump_state("child", _child_node)

	var img := root.get_texture().get_image()
	var err := img.save_png(_out_path)
	if err != OK:
		printerr("ui_capture: save_png failed (%d)" % err)
		quit(1)
		return
	print("ui_capture: wrote %s (%dx%d)" % [_out_path, img.get_width(), img.get_height()])
	quit(0)


func _dump_state(label: String, n: Node) -> void:
	if n == null:
		return
	if n is CanvasItem:
		print("ui_capture: [capture] %s %s visible=%s modulate=%s" % [
			label, n.name, str((n as CanvasItem).visible), str((n as CanvasItem).modulate)])
	for c in n.get_children():
		if c is Control:
			print("ui_capture: [capture]   %s visible=%s modulate=%s rect=%s" % [
				c.name, str((c as Control).visible), str((c as CanvasItem).modulate),
				str((c as Control).get_global_rect())])
