import { GetObjectCommand, HeadObjectCommand, PutObjectCommand, S3Client } from 'npm:@aws-sdk/client-s3@3.1098.0';
import { getSignedUrl } from 'npm:@aws-sdk/s3-request-presigner@3.1098.0';
import { ApiError, rpc } from './db.ts';
const bucket='dmc-turkish-dub-qa-archive',endpoint='https://s3.hf.co/xykeskin';
async function secret(name:string){const value=await rpc('qa_get_platform_secret',{p_name:name});return typeof value==='string'&&value.length?value:null;}
async function client(){const accessKeyId=await secret('HF_S3_ACCESS_KEY'),secretAccessKey=await secret('HF_S3_SECRET_KEY');if(!accessKeyId||!secretAccessKey)throw new ApiError(503,'HF S3 kimlik bilgileri yapılandırılmadı.');return new S3Client({region:'us-east-1',endpoint,forcePathStyle:true,credentials:{accessKeyId,secretAccessKey}});}
export async function ready(){return!!(await secret('HF_S3_ACCESS_KEY'))&&!!(await secret('HF_S3_SECRET_KEY'));}
export async function presign(method:'PUT'|'GET',key:string,expires=3600){const s3=await client();return await getSignedUrl(s3,method==='PUT'?new PutObjectCommand({Bucket:bucket,Key:key}):new GetObjectCommand({Bucket:bucket,Key:key}),{expiresIn:expires});}
export async function headObject(key:string){try{const out=await(await client()).send(new HeadObjectCommand({Bucket:bucket,Key:key}));return{size:Number(out.ContentLength||0)};}catch{throw new ApiError(409,'HF istemci paketi doğrulanamadı.');}}
