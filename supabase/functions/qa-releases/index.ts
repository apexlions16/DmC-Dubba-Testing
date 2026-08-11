import { ApiError, eq, insert, json, one, rows } from './db.ts';
import { authenticate, requireAdmin } from './auth.ts';
import { headObject, presign, ready } from './s3.ts';

function route(req:Request){const p=new URL(req.url).pathname,m='/qa-releases',i=p.indexOf(m);return(i>=0?p.slice(i+m.length):p)||'/';}
async function body(req:Request){try{return await req.json();}catch{return{};}}
function clean(v:string){return v.replace(/[\\/:*?"<>|\x00-\x1f]/g,'_').replace(/\s+/g,' ').trim().slice(0,180)||'client.zip';}
function channel(v:any){const c=String(v||'stable').toLowerCase();if(!['stable','beta'].includes(c))throw new ApiError(400,'Kanal stable veya beta olmalıdır.');return c;}
function validSha(v:any){const s=String(v||'').toLowerCase();if(!/^[0-9a-f]{64}$/.test(s))throw new ApiError(400,'Geçerli SHA-256 gerekli.');return s;}
function artifactUrl(req:Request,id:string){const u=new URL(req.url);return `${u.origin}/functions/v1/qa-releases/client/releases/${id}/download`;}
function manifest(req:Request,x:any){return{id:x.id,channel:x.channel,version:x.version,title:x.title,notes:x.notes,artifact_url:artifactUrl(req,x.id),sha256:x.sha256,minimum_version:x.min_supported_client_version,mandatory:x.mandatory,published_at:x.published_at,size_bytes:x.package_size};}

Deno.serve(async req=>{try{
  if(req.method==='OPTIONS')return new Response(null,{status:204,headers:{'Access-Control-Allow-Origin':'*','Access-Control-Allow-Headers':'authorization,content-type','Access-Control-Allow-Methods':'GET,POST,OPTIONS'}});
  const r=route(req);
  if(r==='/'||r==='/health')return json({status:'ok',s3_ready:await ready()});

  if(r==='/client/releases/latest'&&req.method==='GET'){
    const c=channel(new URL(req.url).searchParams.get('channel')||'stable');
    const x=await one('client_releases',`select=*&channel=${eq(c)}&order=published_at.desc&limit=1`);
    if(!x)throw new ApiError(404,'Bu kanalda yayınlanmış istemci sürümü yok.');
    return json(manifest(req,x));
  }

  let m=r.match(/^\/client\/releases\/([^/]+)\/download$/);
  if(m&&req.method==='GET'){
    const x=await one('client_releases',`select=id,package_storage_path&id=${eq(m[1])}`);
    if(!x)throw new ApiError(404,'İstemci sürümü bulunamadı.');
    return new Response(null,{status:302,headers:{Location:await presign('GET',x.package_storage_path,3600)}});
  }

  const user=await authenticate(req);requireAdmin(user);

  if(r==='/client/releases'&&req.method==='GET'){
    const c=channel(new URL(req.url).searchParams.get('channel')||'stable');
    const list=await rows('client_releases',`select=*&channel=${eq(c)}&order=published_at.desc`);
    return json(list.map((x:any)=>manifest(req,x)));
  }

  if(r==='/client/releases/init'&&req.method==='POST'){
    const x=await body(req);const c=channel(x.channel),version=String(x.version||'').trim(),title=String(x.title||'').trim();
    if(version.length<1||title.length<1)throw new ApiError(400,'Sürüm ve başlık gereklidir.');
    const exists=await one('client_releases',`select=id&channel=${eq(c)}&version=${eq(version)}`);if(exists)throw new ApiError(409,'Bu kanal ve sürüm daha önce yayınlandı.');
    const id=crypto.randomUUID(),filename=clean(String(x.filename||`qa-client-${version}.zip`));
    const storagePath=`_system/client-releases/${c}/${version}/${id}/${filename}`;
    return json({release_id:id,upload_url:await presign('PUT',storagePath,3600),storage_path:storagePath,filename});
  }

  if(r==='/client/releases/finalize'&&req.method==='POST'){
    const x=await body(req);const c=channel(x.channel),version=String(x.version||'').trim(),title=String(x.title||'').trim(),storagePath=String(x.storage_path||'');
    if(!storagePath.startsWith(`_system/client-releases/${c}/${version}/`))throw new ApiError(400,'İstemci paketi storage yolu geçersiz.');
    const expectedSize=Number(x.size_bytes);if(!Number.isFinite(expectedSize)||expectedSize<1)throw new ApiError(400,'Paket boyutu geçersiz.');
    const head=await headObject(storagePath);if(head.size!==Math.floor(expectedSize))throw new ApiError(409,'HF paket boyutu beklenen değerle eşleşmiyor.');
    const record=await insert('client_releases',{id:String(x.release_id||crypto.randomUUID()),version,channel:c,title,notes:String(x.notes||''),package_storage_path:storagePath,sha256:validSha(x.sha256),package_size:Math.floor(expectedSize),min_launcher_version:x.min_launcher_version||null,min_supported_client_version:x.minimum_version||null,mandatory:!!x.mandatory,published_by:user.id});
    return json({id:record.id,channel:record.channel,version:record.version,title:record.title,sha256:record.sha256,size_bytes:record.package_size,artifact_url:artifactUrl(req,record.id)},201);
  }

  throw new ApiError(404,'İstemci sürüm API yolu bulunamadı.');
}catch(e){if(e instanceof ApiError)return json({detail:e.message},e.status);console.error('qa-releases',e);return json({detail:'İstemci sürüm servisinde beklenmeyen bir hata oluştu.'},500);}});
