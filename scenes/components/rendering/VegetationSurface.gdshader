shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
#include "res://scenes/components/rendering/VegetationShared.gdshaderinc"
void vertex(){vegetation_deform(VERTEX,NORMAL,COLOR,MODEL_MATRIX,MODEL_NORMAL_MATRIX);}
void fragment(){vegetation_surface(UV,COLOR,VIEW_MATRIX,NORMAL,ALBEDO,ROUGHNESS,BACKLIGHT);}
