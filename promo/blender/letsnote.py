"""
Portátil antiguo estilo Panasonic Let's note (magnesio plateado, tapa con
relieve, trackpad circular) que se abre y enseña Hakufu en la pantalla.
Renderizado con Cycles, fondo transparente + sombra, para el vídeo de promo.

    /Applications/Blender.app/Contents/MacOS/Blender -b -P blender/letsnote.py -- \
        public/tour/home.png public/laptop [desde] [hasta] [muestras] [off]

Con "off" la pantalla sale apagada (para cerrar la tapa al final: public/laptop-off).

Escribe public/laptop/0000.png … (fotograma a fotograma). La cámara termina
mirando la pantalla de frente con la pantalla ocupando exactamente el marco
de la app en el vídeo (1536×960 centrado en 1920×1080), para cortar sin salto.
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SCREEN_IMG = os.path.abspath(argv[0] if argv else "public/tour/home.png")
OUT = os.path.abspath(argv[1] if len(argv) > 1 else "public/laptop")
F0 = int(argv[2]) if len(argv) > 2 else 0
F1 = int(argv[3]) if len(argv) > 3 else 79
SAMPLES = int(argv[4]) if len(argv) > 4 else 20
FRAMES = 80

# Medidas (1 unidad ≈ 10 cm)
BW, BD, BH = 3.0, 2.1, 0.17           # base
LT = 0.075                            # grosor de la tapa
SW = 2.5                              # ancho de pantalla (16:10)
SH = SW * 10 / 16
FRAME_FRACTION = 1536 / 1920          # la pantalla ocupa esto del ancho al final
LENS, SENSOR = 50.0, 36.0

# ── Escena limpia ──────────────────────────────────────────────────────────────
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.cycles.use_adaptive_sampling = True
try:
    scene.cycles.denoising_use_gpu = True
except AttributeError:
    pass
scene.cycles.denoiser = "OPENIMAGEDENOISE"
scene.cycles.denoising_quality = "BALANCED"   # "HIGH" tarda el triple y no se nota
scene.cycles.adaptive_threshold = 0.04
scene.cycles.max_bounces = 6
scene.cycles.caustics_reflective = False
scene.cycles.caustics_refractive = False
scene.render.use_persistent_data = True     # no reconstruye la escena en cada fotograma
scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
scene.render.film_transparent = True
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "AgX"
scene.view_settings.look = "AgX - Medium High Contrast"
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "METAL"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = True
    scene.cycles.device = "GPU"
except Exception as e:  # sin GPU: CPU
    print("GPU no disponible:", e)


def mat(name, color, metallic=0.0, rough=0.5, coat=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Metallic"].default_value = metallic
    p.inputs["Roughness"].default_value = rough
    if coat:
        p.inputs["Coat Weight"].default_value = coat
        p.inputs["Coat Roughness"].default_value = 0.08
    return m


def magnesium():
    """Magnesio plateado con un granulado fino (como el chorreado de arena)."""
    m = mat("magnesio", (0.60, 0.61, 0.63), metallic=0.92, rough=0.36)
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 900
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.04
    nt.links.new(noise.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], p.inputs["Normal"])
    ramp = nt.nodes.new("ShaderNodeMapRange")
    ramp.inputs["To Min"].default_value = 0.30
    ramp.inputs["To Max"].default_value = 0.42
    nt.links.new(noise.outputs["Fac"], ramp.inputs["Value"])
    nt.links.new(ramp.outputs["Result"], p.inputs["Roughness"])
    return m


M_BODY = magnesium()
M_DARK = mat("plastico_oscuro", (0.025, 0.026, 0.03), rough=0.45)
M_KEY = mat("teclas", (0.035, 0.036, 0.04), rough=0.55)
M_LEGEND = mat("serigrafia", (0.75, 0.75, 0.75), rough=0.6)
M_PAD = mat("pad", (0.33, 0.34, 0.36), metallic=0.3, rough=0.6)
M_RUBBER = mat("goma", (0.02, 0.02, 0.02), rough=0.9)
M_LED = mat("led", (0.1, 1.0, 0.3))
M_LED.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = (0.1, 1.0, 0.35, 1)
M_LED.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 6


def screen_material():
    m = bpy.data.materials.new("pantalla")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(SCREEN_IMG)
    tex.interpolation = "Cubic"
    emis = nt.nodes.new("ShaderNodeEmission")
    emis.name = "emision"
    emis.inputs["Strength"].default_value = 1.35
    nt.links.new(tex.outputs["Color"], emis.inputs["Color"])
    gloss = nt.nodes.new("ShaderNodeBsdfGlossy")
    gloss.inputs["Roughness"].default_value = 0.12
    gloss.inputs["Color"].default_value = (1, 1, 1, 1)
    off = nt.nodes.new("ShaderNodeBsdfDiffuse")
    off.inputs["Color"].default_value = (0.008, 0.009, 0.012, 1)
    mixoff = nt.nodes.new("ShaderNodeMixShader")       # pantalla apagada ↔ encendida
    mixoff.name = "encendido"
    # se controla con una propiedad del objeto (cambiar un valor del material
    # recompila los shaders en cada fotograma y el render va 10 veces más lento)
    # (Object Info → Color: las propiedades personalizadas no se refrescan con datos persistentes)
    info = nt.nodes.new("ShaderNodeObjectInfo")
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(info.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Red"], mixoff.inputs["Fac"])
    nt.links.new(off.outputs[0], mixoff.inputs[1])
    nt.links.new(emis.outputs[0], mixoff.inputs[2])
    # cristal mate: solo un reflejo suave en ángulos rasantes
    fres = nt.nodes.new("ShaderNodeLayerWeight")
    fres.inputs["Blend"].default_value = 0.12
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(fres.outputs["Fresnel"], mix.inputs["Fac"])
    nt.links.new(mixoff.outputs[0], mix.inputs[1])
    nt.links.new(gloss.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    return m


M_SCREEN = screen_material()


def box(name, size, loc, material, bevel=0.0, segs=4, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        b = o.modifiers.new("bisel", "BEVEL")
        b.width = bevel
        b.segments = segs
        b.limit_method = "ANGLE"
        bpy.ops.object.shade_smooth()
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


def cylinder(name, r, depth, loc, material, rot=(0, 0, 0), verts=64, parent=None, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=depth, location=loc, rotation=rot, vertices=verts)
    o = bpy.context.active_object
    o.name = name
    if bevel:
        b = o.modifiers.new("bisel", "BEVEL")
        b.width = bevel
        b.segments = 3
        b.limit_method = "ANGLE"
    bpy.ops.object.shade_smooth()
    o.data.materials.append(material)
    if parent:
        o.parent = parent
    return o


# ── Base ───────────────────────────────────────────────────────────────────────
base = box("base", (BW, BD, BH), (0, 0, BH / 2), M_BODY, bevel=0.06, segs=6)

# Teclado: pozo oscuro y teclas
KB_W, KB_TOP, ROWS, PITCH = 2.72, BD / 2 - 0.16, 6, 0.168
well_d = ROWS * PITCH + 0.04
box("pozo_teclado", (KB_W + 0.04, well_d, 0.02), (0, KB_TOP - well_d / 2, BH - 0.004), M_DARK, bevel=0.01)
layout = [
    [1] * 16,                                 # fila de funciones (más baja)
    [1] * 13 + [2],
    [1.5] + [1] * 12 + [1.5],
    [1.8] + [1] * 11 + [2.2],
    [2.3] + [1] * 10 + [2.7],
    [1.2, 1, 1.2, 1.2, 5.2, 1.2, 1.2, 1, 1, 1],
]
for r, row in enumerate(layout):
    unit = (KB_W - 0.01) / sum(row)
    x = -KB_W / 2
    y = KB_TOP - 0.02 - PITCH * (r + 0.5)
    kh = PITCH * (0.62 if r == 0 else 0.84)
    for w in row:
        kw = unit * w - 0.024
        key = box(f"tecla_{r}_{x:.2f}", (kw, kh, 0.03), (x + unit * w / 2, y, BH + 0.01), M_KEY, bevel=0.008, segs=2)
        # serigrafía: una rayita clara en la esquina de cada tecla
        if w < 3:
            box(f"leyenda_{r}_{x:.2f}", (min(0.04, kw * 0.3), 0.01, 0.002), (x + unit * w / 2 - kw * 0.2, y + kh * 0.18, BH + 0.026), M_LEGEND)
        x += unit * w

# Trackpad circular (wheel pad) con su anillo y dos botones
PAD_Y = -0.52
cylinder("anillo_pad", 0.30, 0.02, (0, PAD_Y, BH - 0.002), M_DARK, bevel=0.006)
cylinder("pad", 0.265, 0.02, (0, PAD_Y, BH + 0.001), M_PAD, bevel=0.004)
bpy.ops.mesh.primitive_torus_add(major_radius=0.285, minor_radius=0.012, location=(0, PAD_Y, BH + 0.008))
tor = bpy.context.active_object
tor.name = "aro_pad"
bpy.ops.object.shade_smooth()
tor.data.materials.append(M_BODY)
for sx in (-1, 1):
    box(f"boton_{sx}", (0.36, 0.11, 0.02), (sx * 0.2, PAD_Y - 0.4, BH + 0.0), M_DARK, bevel=0.02)

# LEDs y puertos laterales
for i in range(4):
    box(f"led_{i}", (0.03, 0.012, 0.006), (-1.25 + i * 0.07, -BD / 2 + 0.12, BH + 0.001), M_LED if i == 0 else M_DARK)
for i, w in enumerate([0.16, 0.12, 0.12, 0.22]):
    box(f"puerto_{i}", (0.02, w, 0.06), (BW / 2 + 0.002, 0.6 - i * 0.3, BH / 2), M_DARK, bevel=0.005)
for sx in (-1, 1):
    for sy in (-1, 1):
        cylinder(f"pata_{sx}{sy}", 0.06, 0.02, (sx * (BW / 2 - 0.2), sy * (BD / 2 - 0.2), -0.008), M_RUBBER)
# bisagra
cylinder("bisagra", 0.055, BW - 0.6, (0, BD / 2 - 0.02, BH + 0.035), M_DARK, rot=(0, math.pi / 2, 0), bevel=0.01)

# ── Tapa (se abre girando sobre la bisagra) ────────────────────────────────────
hinge = bpy.data.objects.new("eje_tapa", None)
scene.collection.objects.link(hinge)
hinge.location = (0, BD / 2 - 0.02, BH + 0.035)

LD = BD - 0.02
lid = box("tapa", (BW, LD, LT), (0, -LD / 2, LT / 2), M_BODY, bevel=0.05, segs=6, parent=hinge)
# relieve "bonnet" de la parte de fuera: panel abombado con dos escalones
box("capo_1", (BW - 0.5, LD - 0.45, 0.03), (0, -LD / 2 - 0.02, LT + 0.005), M_BODY, bevel=0.1, segs=8, parent=hinge)
box("capo_2", (BW - 1.1, LD - 0.95, 0.03), (0, -LD / 2 - 0.05, LT + 0.022), M_BODY, bevel=0.14, segs=8, parent=hinge)
# marco y pantalla (cara de abajo cuando está cerrada)
box("marco", (BW - 0.08, LD - 0.08, 0.01), (0, -LD / 2, -0.003), M_DARK, bevel=0.03, parent=hinge)
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, -LD / 2 - 0.02, -0.0095))
scr = bpy.context.active_object
scr.name = "pantalla"
scr.scale = (SW, SH, 1)
scr.rotation_euler = (math.pi, 0, 0)        # mira hacia abajo (-Z) con la imagen derecha
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
scr.data.materials.append(M_SCREEN)
scr.parent = hinge
# gomas del marco
for sx in (-1, 1):
    box(f"goma_marco_{sx}", (0.08, 0.04, 0.01), (sx * 1.3, -LD + 0.12, -0.009), M_RUBBER, parent=hinge)

SCREEN_LOCAL = Vector((0, -LD / 2 - 0.02, -0.0095))

# ── Suelo (solo sombra) y luces de estudio ─────────────────────────────────────
bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, -0.018))
floor = bpy.context.active_object
floor.is_shadow_catcher = True
floor.data.materials.append(mat("suelo", (0.9, 0.9, 0.9), rough=0.8))


def area(name, loc, size, energy, color=(1, 1, 1), target=(0, 0, 0.5)):
    d = bpy.data.lights.new(name, "AREA")
    d.size = size
    d.energy = energy
    d.color = color
    o = bpy.data.objects.new(name, d)
    scene.collection.objects.link(o)
    o.location = loc
    direction = Vector(target) - Vector(loc)
    o.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    return o


area("principal", (-4, -4, 7), 5, 900)
area("relleno", (5, -3, 3), 6, 260, color=(0.88, 0.93, 1.0))
area("contra", (2, 6, 5), 4, 700, color=(0.75, 0.85, 1.0))
area("tira_reflejo", (0, -6, 2.5), 3, 180)
world = bpy.data.worlds.new("mundo")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
sky = world.node_tree.nodes.new("ShaderNodeTexGradient")
coord = world.node_tree.nodes.new("ShaderNodeTexCoord")
sep = world.node_tree.nodes.new("ShaderNodeSeparateXYZ")
ramp = world.node_tree.nodes.new("ShaderNodeValToRGB")
world.node_tree.links.new(coord.outputs["Generated"], sep.inputs[0])
world.node_tree.links.new(sep.outputs["Z"], ramp.inputs["Fac"])
ramp.color_ramp.elements[0].position = 0.45
ramp.color_ramp.elements[0].color = (0.25, 0.26, 0.28, 1)
ramp.color_ramp.elements[1].position = 0.75
ramp.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1)
world.node_tree.links.new(ramp.outputs["Color"], bg.inputs["Color"])
bg.inputs["Strength"].default_value = 0.55

# ── Cámara y animación ─────────────────────────────────────────────────────────
cam_data = bpy.data.cameras.new("camara")
cam_data.lens = LENS
cam_data.sensor_width = SENSOR
cam_data.sensor_fit = "HORIZONTAL"
cam_data.dof.use_dof = True
cam_data.dof.aperture_fstop = 5.6
cam = bpy.data.objects.new("camara", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

OPEN_DEG = 105.0


def smooth(t):
    t = min(1, max(0, t))
    return t * t * (3 - 2 * t)


def ease_in_out(t):
    t = min(1, max(0, t))
    return 4 * t ** 3 if t < 0.5 else 1 - (-2 * t + 2) ** 3 / 2


def screen_pose(theta_deg):
    """Centro y normal de la pantalla en el mundo para una apertura dada."""
    hinge.rotation_euler = (-math.radians(theta_deg), 0, 0)
    bpy.context.view_layer.update()
    mw = hinge.matrix_world
    centre = mw @ SCREEN_LOCAL
    normal = (mw.to_3x3() @ Vector((0, 0, -1))).normalized()
    return centre, normal


def look(at, frm):
    cam.location = frm
    cam.rotation_euler = (Vector(at) - Vector(frm)).to_track_quat("-Z", "Y").to_euler()


def set_frame(f):
    t = f / (FRAMES - 1)
    # 0–0,55: la tapa se abre (con un pequeño rebote al final)
    o = ease_in_out(t / 0.55)
    theta = OPEN_DEG * o + (2.5 * math.sin(min(1, max(0, (t - 0.5) / 0.15)) * math.pi) if t > 0.5 else 0)
    centre, normal = screen_pose(theta)
    # pantalla: se enciende con un parpadeo cuando la tapa va por la mitad
    on = 0.0 if t < 0.28 else (0.4 if t < 0.31 else (0.0 if t < 0.33 else 1.0))
    scr.color = (on, on, on, 1)
    # cámara: de un 3/4 alto → de frente → empuja hasta que la pantalla llena el marco
    d_final = SW / (2 * FRAME_FRACTION * math.tan(math.atan(SENSOR / 2 / LENS)))
    final_c, final_n = screen_pose(OPEN_DEG)
    screen_pose(theta)
    end_pos = final_c + final_n * d_final
    start_pos = Vector((4.6, -6.2, 5.0))
    start_at = Vector((0, 0.1, 0.4))
    mid_pos = Vector((1.2, -6.6, 2.6))
    mid_at = Vector((0, 0.3, 0.9))
    if t < 0.5:
        k = ease_in_out(t / 0.5)
        pos = start_pos.lerp(mid_pos, k)
        at = start_at.lerp(mid_at, k)
    else:
        k = ease_in_out((t - 0.5) / 0.5)
        pos = mid_pos.lerp(end_pos, k)
        at = mid_at.lerp(final_c, k)
    look(at, pos)
    cam_data.dof.focus_distance = (Vector(at) - pos).length
    cam_data.dof.use_dof = t < 0.95


# Teclas y piezas fijas de la base en una sola malla: menos objetos que sincronizar.
bpy.ops.object.select_all(action="DESELECT")
fixed = [o for o in scene.objects if o.type == "MESH" and o.parent is None and o.name != "base" and not o.is_shadow_catcher]
for o in fixed:
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)
bpy.ops.object.join()

SCREEN_OFF = len(argv) > 5 and argv[5] == "off"


def on_frame(sc, *_):
    set_frame(sc.frame_current)
    if SCREEN_OFF:
        scr.color = (0, 0, 0, 1)


bpy.app.handlers.frame_change_pre.append(on_frame)
os.makedirs(OUT, exist_ok=True)
scene.frame_start, scene.frame_end = F0, F1
scene.frame_step = 3 if SCREEN_OFF else 1   # el cierre va rápido: basta uno de cada tres
scene.render.filepath = os.path.join(OUT, "")
bpy.ops.render.render(animation=True)
