const fs=require('fs'); const path=require('path'); const sharp=require('sharp');
const root=process.argv[2] || path.resolve(__dirname,'..');
const svgDir=path.join(root,'svg');
async function render(source,out,w,h,opaque=false){
  let a=sharp(source).resize(w,h,{fit:'fill'});
  if(opaque)a=a.flatten({background:'#F4CD3C'}).removeAlpha();
  await a.png().toFile(out);
}
async function main(){
 for(const name of fs.readdirSync(svgDir).filter(n=>n.endsWith('.svg'))){
   const src=path.join(svgDir,name); const meta=await sharp(src).metadata();
   await render(src,path.join(root,'png',name.replace('.svg','.png')),meta.width,meta.height,name.startsWith('app-icon-master'));
 }
 const icon=path.join(svgDir,'app-icon-master-v1.svg');
 const fav=path.join(svgDir,'favicon-v1.svg');
 for(const n of [16,32,48,64])await render(fav,path.join(root,'web',`favicon-${n}.png`),n,n,true);
 for(const n of [192,512]){
   await render(icon,path.join(root,'web',`icon-${n}.png`),n,n,true);
   await render(path.join(svgDir,'web-maskable-v1.svg'),path.join(root,'web',`icon-maskable-${n}.png`),n,n,true);
 }
 await render(icon,path.join(root,'web','apple-touch-icon.png'),180,180,true);
 fs.copyFileSync(fav,path.join(root,'web','favicon.svg'));
 // ICO directory embeds standard PNG frames, no quality loss from an intermediate screenshot.
 const frames=[16,32,48].map(n=>({n,data:fs.readFileSync(path.join(root,'web',`favicon-${n}.png`))}));
 const header=Buffer.alloc(6+16*frames.length);header.writeUInt16LE(1,2);header.writeUInt16LE(frames.length,4);
 let offset=header.length;
 frames.forEach(({n,data},i)=>{const k=6+16*i;header[k]=n;header[k+1]=n;header.writeUInt16LE(1,k+4);header.writeUInt16LE(32,k+6);header.writeUInt32LE(data.length,k+8);header.writeUInt32LE(offset,k+12);offset+=data.length;});
 fs.writeFileSync(path.join(root,'web','favicon.ico'),Buffer.concat([header,...frames.map(x=>x.data)]));
 const ios=path.join(root,'ios','AppIcon.appiconset');fs.mkdirSync(ios,{recursive:true});
 await render(icon,path.join(ios,'AppIcon-1024.png'),1024,1024,true);
 fs.writeFileSync(path.join(ios,'Contents.json'),JSON.stringify({images:[{filename:'AppIcon-1024.png',idiom:'universal',platform:'ios',size:'1024x1024'}],info:{author:'xcode',version:1}},null,2));
 await render(icon,path.join(root,'android','google-play-icon-512.png'),512,512,true);
 for(const [density,factor,legacy] of [['mdpi',1,48],['hdpi',1.5,72],['xhdpi',2,96],['xxhdpi',3,144],['xxxhdpi',4,192]]){
   const dir=path.join(root,'android','res',`mipmap-${density}`);fs.mkdirSync(dir,{recursive:true});
   await render(icon,path.join(dir,'tdf_launcher.png'),legacy,legacy,true);
   for(const layer of ['foreground','background','monochrome'])await render(path.join(svgDir,`android-adaptive-${layer}-v1.svg`),path.join(dir,`tdf_${layer}.png`),108*factor,108*factor,layer==='background');
 }
 for(const api of [26,33]){
   const dir=path.join(root,'android','res',`mipmap-anydpi-v${api}`);fs.mkdirSync(dir,{recursive:true});
   fs.writeFileSync(path.join(dir,'tdf_launcher.xml'),`<?xml version="1.0" encoding="utf-8"?>\n<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">\n  <background android:drawable="@mipmap/tdf_background"/>\n  <foreground android:drawable="@mipmap/tdf_foreground"/>\n${api===33?'  <monochrome android:drawable="@mipmap/tdf_monochrome"/>\n':''}</adaptive-icon>\n`);
 }
 fs.writeFileSync(path.join(root,'web','manifest-icons.fragment.json'),JSON.stringify({name:'That Damn Fly',short_name:'That Damn Fly',icons:[{src:'/brand/icon-192.png',sizes:'192x192',type:'image/png',purpose:'any'},{src:'/brand/icon-512.png',sizes:'512x512',type:'image/png',purpose:'any'},{src:'/brand/icon-maskable-192.png',sizes:'192x192',type:'image/png',purpose:'maskable'},{src:'/brand/icon-maskable-512.png',sizes:'512x512',type:'image/png',purpose:'maskable'}]},null,2));
 fs.writeFileSync(path.join(root,'web','head.fragment.html'),'<link rel="icon" href="/brand/favicon.ico" sizes="any">\n<link rel="icon" href="/brand/favicon.svg" type="image/svg+xml">\n<link rel="apple-touch-icon" href="/brand/apple-touch-icon.png">\n<meta name="theme-color" content="#F4CD3C">\n');
 await render(path.join(root,'docs','Brand_Kit_Preview.svg'),path.join(root,'docs','Brand_Kit_Preview.png'),1600,1260);
 // Visual QA grid uses actual assets, with dark backgrounds behind reverse variants.
 const cards=[]; let i=0;
 for(const type of ['stacked','horizontal'])for(const v of ['color','reverse','mono-dark','mono-light']){
   const src=path.join(svgDir,`logo-${type}-${v}-v1.svg`);const dark=['reverse','mono-light'].includes(v);
   const img=await sharp(src).resize(680,210,{fit:'contain',background:dark?'#202622':'#F7F0DE'}).flatten({background:dark?'#202622':'#F7F0DE'}).png().toBuffer();
   cards.push({input:img,left:(i%2)*700+10,top:Math.floor(i/2)*230+10});i++;
 }
 await sharp({create:{width:1400,height:920,channels:3,background:'#dedbd0'}}).composite(cards).png().toFile(path.join(root,'docs','Variants_Preview.png'));
 const report=[];
 for(const rel of ['png/logo-stacked-color-v1.png','png/logo-horizontal-color-v1.png','ios/AppIcon.appiconset/AppIcon-1024.png','android/google-play-icon-512.png','web/favicon-16.png','web/icon-maskable-512.png']){const meta=await sharp(path.join(root,rel)).metadata();report.push({file:rel,width:meta.width,height:meta.height,alpha:meta.hasAlpha});}
 fs.writeFileSync(path.join(root,'docs','export-check.json'),JSON.stringify(report,null,2));
 console.log(JSON.stringify(report,null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
