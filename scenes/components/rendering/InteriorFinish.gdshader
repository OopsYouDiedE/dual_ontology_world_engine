shader_type spatial;
render_mode cull_back;
// Metre-scale authoring finishes. Geometry supplies reveals, profiles and panelling.
uniform vec4 tint : source_color = vec4(0.8, 0.75, 0.65, 1.0);
uniform vec4 secondary : source_color = vec4(0.18, 0.17, 0.15, 1.0);
uniform float roughness_value = 0.75;
uniform float roughness_variation : hint_range(0.0, 0.5) = 0.04;
// Optional micro-surface pass for A/B authoring studies; zero preserves legacy output.
uniform float detail_strength : hint_range(0.0, 1.0) = 0.0;
uniform float detail_scale : hint_range(0.1, 8.0) = 1.0;
uniform float toon_strength : hint_range(0.0, 1.0) = 0.0;
uniform float metallic_value : hint_range(0.0, 1.0) = 0.0;
uniform float specular_value : hint_range(0.0, 1.0) = 0.5;
uniform float clearcoat_value : hint_range(0.0, 1.0) = 0.0;
uniform float clearcoat_roughness_value : hint_range(0.0, 1.0) = 0.15;
uniform float anisotropy_value : hint_range(0.0, 1.0) = 0.0;
uniform float micro_height_m = 0.00005;
uniform float seam_depth_m = 0.0004;
uniform float normal_strength : hint_range(0.0, 2.0) = 1.0;
uniform float ao_strength : hint_range(0.0, 1.0) = 0.15;
// Optional authored maps. Normal/ORM/height are linear data, never source_color.
uniform bool use_normal_texture = false;
uniform bool use_orm_texture = false;
uniform bool use_height_texture = false;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D orm_texture : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D height_texture : filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec2 map_size_m = vec2(1.0);
uniform float normal_map_strength : hint_range(0.0, 2.0) = 1.0;
uniform int finish = 0; // plaster, timber, checker marble, limestone, slate, cloth, marble
uniform float module_m = 0.8;
uniform bool hall_border = false;
uniform vec2 border_center_m = vec2(0.0);
uniform vec2 border_inner_half_m = vec2(5.35, 7.35);
uniform float cutoff = 100000.0;
uniform bool use_cutoff = false;
varying vec3 wp;
varying vec3 wn;
float hash21(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float noise2(vec2 p) {
 vec2 i=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);
 return mix(mix(hash21(i),hash21(i+vec2(1,0)),f.x),mix(hash21(i+vec2(0,1)),hash21(i+vec2(1,1)),f.x),f.y);
}
float edge_line(float v,float width) {
 float d=min(fract(v),1.0-fract(v));
 return 1.0-smoothstep(width,width+max(fwidth(v),0.001),d);
}
void vertex() {
 wp=(MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
 wn=normalize(MODEL_NORMAL_MATRIX*NORMAL);
}
void light() {
 float ndl = max(dot(NORMAL, LIGHT), 0.0);
 float band_low = smoothstep(0.20, 0.34, ndl);
 float band_high = smoothstep(0.58, 0.72, ndl);
 float toon_diffuse = 0.34 + band_low * 0.30 + band_high * 0.36;
 float diffuse_term = mix(ndl, toon_diffuse, toon_strength);
 DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR * diffuse_term * ATTENUATION;
 float metal_spec = pow(max(dot(NORMAL, normalize(LIGHT + VIEW)), 0.0), mix(48.0, 10.0, ROUGHNESS));
 SPECULAR_LIGHT += LIGHT_COLOR * metal_spec * metallic_value * 0.16 * ATTENUATION;
}
void fragment() {
 if(use_cutoff && wp.y>cutoff) discard;
 vec3 n=abs(wn);
 vec2 p=n.y>0.65?wp.xz:(n.x>n.z?vec2(wp.z,wp.y):wp.xy);
 vec3 c=tint.rgb;
 float detail_macro=noise2(p*(0.75*detail_scale));
 float detail_fine=noise2(p*(9.0*detail_scale));
 float detail_pores=noise2(p*(42.0*detail_scale));
 float detail_albedo=(detail_macro-0.5)*0.12+(detail_fine-0.5)*0.05+(detail_pores-0.5)*0.02;
 float small_detail=1.0-smoothstep(0.015,0.09,max(length(dFdx(p)),length(dFdy(p))));
 float grain=noise2(p*vec2(8.0,110.0));
 float joint_mask=0.0;
 float height_m=(noise2(p*90.0)-0.5)*micro_height_m*small_detail;
 float local_roughness=roughness_value+roughness_variation*(noise2(p*3.7)-0.5)+(detail_fine-0.5)*0.09*detail_strength;
 height_m+=(detail_pores-0.5)*0.00022*detail_strength;
 if(finish==0) {
  c*=0.97+0.045*noise2(p*2.0)+0.018*(noise2(p*90.0)-0.5)*small_detail;
 } else if(finish==1) {
  vec2 t=p/vec2(0.18,1.5); t.y+=mod(floor(t.x),3.0)*0.33;
  float seam=max(edge_line(t.x,0.015),edge_line(t.y,0.004));
  float variation=hash21(floor(t));
  c*=0.88+variation*0.17+0.07*(grain-0.5)*small_detail;
  c=mix(c,secondary.rgb,seam*0.55);
  if(n.y<0.65) {
   c=tint.rgb*(0.94+0.08*grain*small_detail);
   c=mix(c,secondary.rgb,edge_line(p.x/0.6,0.004)*0.3);
   seam=edge_line(p.x/0.6,0.004);
  }
  joint_mask=seam;
  height_m+=(grain-0.5)*micro_height_m*small_detail-seam*seam_depth_m;
  local_roughness+=0.04*(grain-0.5)*small_detail+seam*0.12;
 } else if(finish==2) {
  vec2 t=p/module_m;
  float checker=mod(floor(t.x)+floor(t.y),2.0);
  c=mix(tint.rgb,secondary.rgb,checker);
  if(hall_border && any(greaterThan(abs(wp.xz-border_center_m),border_inner_half_m))) c=tint.rgb*0.94;
  float joint=max(edge_line(t.x,0.003),edge_line(t.y,0.003));
  c*=0.975+0.035*noise2(p*2.5);
  c=mix(c,vec3(0.34,0.31,0.26),joint*0.28);
  float veins=1.0-smoothstep(0.018,0.065,abs(sin(p.x*2.7+p.y*1.4+noise2(p*0.65)*5.0)));
  c=mix(c,c*0.78,veins*0.45);
  joint_mask=joint;
  height_m-=joint*seam_depth_m+veins*micro_height_m*0.15;
  local_roughness+=joint*0.22+veins*0.008;
 } else if(finish==3) {
  vec2 t=p/vec2(0.8,0.6);t.x+=mod(floor(t.y),2.0)*0.5;
  c*=0.95+0.065*noise2(p*5.0);
  joint_mask=max(edge_line(t.x,0.003),edge_line(t.y,0.003));
  c*=1.0-joint_mask*0.12;
  height_m-=joint_mask*seam_depth_m;
 } else if(finish==4) {
  vec2 t=p/vec2(0.3,0.22);t.x+=mod(floor(t.y),2.0)*0.5;
  c*=0.85+hash21(floor(t))*0.22;
  joint_mask=max(edge_line(t.x,0.014),edge_line(t.y,0.014));
  c*=1.0-joint_mask*0.3;
  height_m-=joint_mask*seam_depth_m;
 } else if(finish==6) {
  vec2 t=p/1.0;
  float wave=p.x*2.1+p.y*0.9+noise2(p*0.38)*5.0+noise2(p*2.1)*0.5;
  float veins=1.0-smoothstep(0.012,0.07,abs(sin(wave)));
  c=mix(tint.rgb,secondary.rgb,veins*0.48);
  c*=0.975+0.035*noise2(p*1.3);
  float joint=max(edge_line(t.x,0.0015),edge_line(t.y,0.0015));
  c*=1.0-joint*0.14;
  joint_mask=joint;
  height_m-=joint*seam_depth_m+veins*micro_height_m*0.15;
  local_roughness+=joint*0.22+veins*0.008;
 } else {
  c*=0.98+0.03*(grain-0.5)*small_detail;
 }
 c*=1.0+detail_albedo*detail_strength;
 ALBEDO=c;
 vec2 map_uv=p/max(map_size_m,vec2(0.001));
 if(use_height_texture) height_m+=(texture(height_texture,map_uv).r-0.5)*micro_height_m;
 // Screen derivatives perturb the view-space normal in physical metres.
 // This is bump shading only; physical profiles remain real generated geometry.
 vec3 dpdx=dFdx(VERTEX),dpdy=dFdy(VERTEX);
 vec3 r1=cross(dpdy,NORMAL),r2=cross(NORMAL,dpdx);
 float det=dot(dpdx,r1);
 vec3 gradient=sign(det)*(r1*dFdx(height_m)+r2*dFdy(height_m))/max(abs(det),0.00000001);
 NORMAL=normalize(NORMAL-normal_strength*gradient);
 if(use_normal_texture) {
  vec3 tn=texture(normal_texture,map_uv).rgb*2.0-1.0;
  tn.xy*=normal_map_strength;
  NORMAL=normalize(TANGENT*tn.x+BINORMAL*tn.y+NORMAL*tn.z);
 }
 float occlusion=1.0-joint_mask*ao_strength;
 float metal=metallic_value;
 if(use_orm_texture) {
  vec3 orm=texture(orm_texture,map_uv).rgb;
  occlusion*=orm.r;local_roughness=orm.g;metal=orm.b;
 }
 ROUGHNESS=clamp(local_roughness,0.06,1.0);
 METALLIC=metal;
 SPECULAR=specular_value;
 CLEARCOAT=clearcoat_value;
 CLEARCOAT_ROUGHNESS=clearcoat_roughness_value;
 ANISOTROPY=anisotropy_value;
 ANISOTROPY_FLOW=vec2(0.0,1.0);
 AO=occlusion;
 AO_LIGHT_AFFECT=0.15;
}
