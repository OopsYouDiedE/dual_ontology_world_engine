uniform vec4 tint : source_color = vec4(0.8, 0.76, 0.65, 1.0);
uniform int pattern = 0;
uniform float roughness_value = 0.85;
uniform float cutoff = 100000.0;
uniform bool use_cutoff = false;
// Optional wall layers. Unset layers preserve the procedural surface.
uniform bool use_base_texture = false;
uniform bool use_detail_texture = false;
uniform bool use_mask_texture = false;
uniform sampler2D base_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D detail_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D mask_texture : filter_linear_mipmap, repeat_enable;
uniform vec2 texture_scale = vec2(1.0);
uniform float detail_blend : hint_range(0.0, 1.0) = 0.5;
varying vec3 wp;
float noise_cell(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float seam(float x,float width) { float d=min(fract(x),1.0-fract(x)); return 1.0-smoothstep(width,width+max(fwidth(x),0.001),d); }
void vertex() { wp=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz; }
void fragment() {
 if(use_cutoff && wp.y>cutoff) discard;
 vec3 color=tint.rgb;
 vec2 uv=UV;
 if(pattern==1) {
  float row=floor(uv.y/0.23);
  float plank=uv.x/1.8+mod(row,3.0)*0.33;
  color*=0.91+noise_cell(vec2(floor(plank),row))*0.17;
  color*=1.0-0.17*max(seam(uv.y/0.23,0.013),seam(plank,0.005));
  color*=0.98+0.02*sin(uv.x*95.0+sin(uv.x*9.0)*2.0+row*17.0);
 } else if(pattern==2) {
  color*=1.0-0.18*max(seam(uv.x/0.65,0.012),seam(uv.y/0.65,0.012));
  color*=0.97+0.05*noise_cell(floor(uv/0.65));
 } else if(pattern==3) {
  color*=0.96+0.06*noise_cell(floor(uv*85.0));
  color*=1.0-0.1*max(seam(uv.x,0.008),seam(uv.y,0.008));
 } else if(pattern==4) {
  color*=0.98+0.02*sin(uv.x*32.0);
  if(mod(uv.y,3.58)<0.9) {color=mix(color,vec3(0.82,0.79,0.69),0.36);color*=1.0-0.12*seam(uv.x/0.48,0.014);}
  if(abs(mod(uv.y,3.58)-0.9)<0.027) color=mix(color,vec3(0.9,0.87,0.78),0.65);
 } else if(pattern==5) {
  color*=1.0-0.12*max(seam(uv.y/0.26,0.015),seam(uv.x/0.7+mod(floor(uv.y/0.26),2.0)*0.5,0.012));
 } else if(pattern==6) { color*=0.94+0.08*noise_cell(floor(uv*120.0));
 } else if(pattern==7) { color*=0.62+0.38*mod(floor(uv.x/0.65)+floor(uv.y/0.65),2.0); }
 vec2 tex_uv=UV*texture_scale;
 if(use_base_texture) color*=texture(base_texture,tex_uv).rgb;
 if(use_detail_texture) {
  float blend=detail_blend;
  if(use_mask_texture) blend*=texture(mask_texture,tex_uv).r;
  color=mix(color,texture(detail_texture,tex_uv).rgb,blend);
 }
 ALBEDO=color;
 ROUGHNESS=roughness_value;
}
