import { CopyObjectCommand, DeleteObjectCommand, GetObjectCommand, HeadObjectCommand, PutObjectCommand, S3Client } from 'npm:@aws-sdk/client-s3@3.1098.0';
import { getSignedUrl } from 'npm:@aws-sdk/s3-request-presigner@3.1098.0';
import { ApiError, rpc } from './db.ts';

const bucket='dmc-turkish-dub-qa-archive';
const endpoint='https://s3.hf.co/xykeskin';
async function secret(name:string){const value=await rpc('qa_get_platform_secret',{p_name:name});return typeof value==='string'&&value.length?value:null;}
async function client(){const accessKeyId=await secret('HF_S3_ACCESS_KEY'),secretAccessKey=await secret('HF_S3_SECRET_KEY');if(!accessKeyId||!secretAccessKey)throw new ApiError(503,'HF S3 kimlik bilgileri henüz yapılandırılmadı.');return new S3Client({region:'us-east-1',endpoint,forcePathStyle:true,credentials:{accessKeyId,secretAccessKey}});}
export async function ready(){return!!(await secret('HF_S3_ACCESS_KEY'))&&!!(await secret('HF_S3_SECRET_KEY'));}
export async function presign(method:'PUT'|'GET',key:string,expires=3600){const s3=await client();const command=method==='PUT'?new PutObjectCommand({Bucket:bucket,Key:key}):new GetObjectCommand({Bucket:bucket,Key:key});return await getSignedUrl(s3,command,{expiresIn:expires});}
export async function headObject(key:string){const s3=await client();try{const out=await s3.send(new HeadObjectCommand({Bucket:bucket,Key:key}));return{size:Number(out.ContentLength||0),etag:out.ETag||null};}catch{throw new ApiError(409,'HF üzerinde yüklenen dosya doğrulanamadı.');}}
export async function moveObject(sourceKey:string,destinationKey:string){const s3=await client();try{await s3.send(new CopyObjectCommand({Bucket:bucket,Key:destinationKey,CopySource:`${bucket}/${sourceKey}`}));await s3.send(new HeadObjectCommand({Bucket:bucket,Key:destinationKey}));await s3.send(new DeleteObjectCommand({Bucket:bucket,Key:sourceKey}));}catch(e){console.error('HF CopyObject',e);throw new ApiError(502,'Build dosyası HF arşiv alanına taşınamadı.');}}
