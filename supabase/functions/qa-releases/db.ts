export class ApiError extends Error{status:number;constructor(status:number,message:string){super(message);this.status=status;}}
const base=Deno.env.get('SUPABASE_URL')!,service=Deno.env.get('SUPABASE_SERVICE_ROLE_KEY')!;
function headers(extra:Record<string,string>={}){return{apikey:service,Authorization:`Bearer ${service}`,'Content-Type':'application/json',...extra};}
export async function rest(path:string,init:RequestInit={}){const r=await fetch(`${base}/rest/v1/${path}`,{...init,headers:{...headers(),...(init.headers as Record<string,string>||{})}});if(!r.ok){let d=`Veritabanı isteği başarısız (${r.status}).`;try{const p=await r.json();d=p.message||p.details||d;}catch{}throw new ApiError(r.status>=500?502:r.status,d);}if(r.status===204)return null;const t=await r.text();return t?JSON.parse(t):null;}
export async function rows(table:string,q=''):Promise<any[]>{return await rest(`${table}${q?'?'+q:''}`)||[];}
export async function one(table:string,q:string){const d=await rows(table,q);return d[0]??null;}
export async function insert(table:string,b:any){const d=await rest(table,{method:'POST',headers:{Prefer:'return=representation'},body:JSON.stringify(b)});return Array.isArray(d)?d[0]:d;}
export async function rpc(name:string,b:any){return await rest(`rpc/${name}`,{method:'POST',body:JSON.stringify(b)});}
export const enc=(s:string)=>encodeURIComponent(s);export const eq=(s:string)=>`eq.${enc(s)}`;
export function json(data:any,status=200,extra:Record<string,string>={}){return new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json; charset=utf-8',...extra}});}
