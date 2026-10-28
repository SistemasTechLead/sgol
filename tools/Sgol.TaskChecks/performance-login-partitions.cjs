// Separate real loopback source addresses demonstrate whether Razor keeps the caller's limiter partition.
const https=require('https'),fs=require('fs');const results=[];
function session(family,localAddress,extraHeaders={}){const cookies=new Map();return async(path,method='GET',data=null,form=false)=>new Promise((resolve,reject)=>{
 const payload=data===null?null:Object.fromEntries(Object.entries(data).filter(([key])=>key!=='__csrf'));
 const body=data===null?null:form?new URLSearchParams(data).toString():JSON.stringify(payload);
 const req=https.request({hostname:'localhost',port:7267,path,method,family,localAddress,rejectUnauthorized:false,headers:{...extraHeaders,Cookie:[...cookies].map(([k,v])=>k+'='+v).join('; '),...(body?{'Content-Type':form?'application/x-www-form-urlencoded':'application/json','Content-Length':Buffer.byteLength(body)}:{}),...(!form&&data?.__csrf?{'X-CSRF-TOKEN':data.__csrf}:{})}},r=>{for(const value of r.headers['set-cookie']||[]){const pair=value.split(';')[0],index=pair.indexOf('=');if(pair.slice(index+1)==='')cookies.delete(pair.slice(0,index));else cookies.set(pair.slice(0,index),pair.slice(index+1));}let text='';r.on('data',b=>text+=b);r.on('end',()=>resolve({status:r.statusCode,text,retryAfter:r.headers['retry-after']}));});req.on('error',reject);req.setTimeout(10000,()=>req.destroy(Error('probe timeout')));if(body)req.write(body);req.end();
 });}
(async()=>{
 // Exhaust both possible internal-loopback partitions. API calls below carry a real anonymous CSRF token.
 for(const [family,ip] of [[4,'127.0.0.1'],[6,'::1']]){const send=session(family,ip);const csrf=JSON.parse((await send('/api/v1/auth/csrf')).text).data.requestToken;
  const statuses=[];for(let i=0;i<11;i++){const r=await send('/api/v1/auth/login','POST',{userName:'unknown-partition-'+i,password:'Invalid synthetic password 2026!',__csrf:csrf});statuses.push(r.status);}results.push({scenario:'exhaust-loopback',source:ip,statuses});
 }
 const send=session(4,'127.0.0.2');const csrf=JSON.parse((await send('/api/v1/auth/csrf')).text).data.requestToken;
 const api=await send('/api/v1/auth/login','POST',{userName:'other-source-user',password:'Invalid synthetic password 2026!',__csrf:csrf});
 // Obtain an actual Razor form token, not a fabricated one.
 const html=await send('/acceso');const token=html.text.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
 const razor=await send('/acceso','POST',{__RequestVerificationToken:token,stepAction:'login',userName:'other-source-user',password:'Invalid synthetic password 2026!'},true);
 results.push({scenario:'fresh-source-api-versus-razor',source:'127.0.0.2',directApiStatus:api.status,razorStatus:razor.status,razorShowsRateLimited:razor.text.includes('Demasiadas solicitudes'),razorShowsCredentialFailure:razor.text.includes('No se pudo iniciar sesi'),errorMarkers:['Demasiadas solicitudes','No se pudo continuar','No se pudo verificar la solicitud','Credenciales','Recarga la página'].filter(x=>razor.text.includes(x)),scope:'Same external source; API has its own unused quota while Razor internal API may use an exhausted server-loopback quota'});
 const forged=session(4,'127.0.0.1',{'X-Forwarded-For':'203.0.113.77','X-SGOL-Login-Ticket':'a'.repeat(64)});const forgedCsrf=JSON.parse((await forged('/api/v1/auth/csrf')).text).data.requestToken;
 const forgedReply=await forged('/api/v1/auth/login','POST',{userName:'forged-ip-user',password:'Invalid synthetic password 2026!',__csrf:forgedCsrf});
 results.push({scenario:'forged-origin-cannot-bypass-exhausted-quota',status:forgedReply.status,passed:forgedReply.status===429});
 const natStatuses=[];for(let i=0;i<8;i++)natStatuses.push((await send('/api/v1/auth/login','POST',{userName:'same-source-extra-'+i,password:'Invalid synthetic password 2026!',__csrf:csrf})).status);
 const quotaHtml=await send('/acceso');const quotaToken=quotaHtml.text.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)[1];
 const quotaRazor=await send('/acceso','POST',{__RequestVerificationToken:quotaToken,stepAction:'login',userName:'over-quota-source',password:'Invalid synthetic password 2026!'},true);
 results.push({scenario:'api-and-razor-share-original-client-quota',natStatuses,razorStatus:quotaRazor.status,retryAfter:quotaRazor.retryAfter,visibleLimit:quotaRazor.text.includes('Demasiadas solicitudes')});
 const invalid=session(4,'127.0.0.3');const invalidCsrf=await invalid('/api/v1/auth/login','POST',{userName:'missing-csrf',password:'Invalid synthetic password 2026!'});
 results.push({scenario:'csrf-still-required',status:invalidCsrf.status});
 if(api.status!==401||razor.status!==200||!razor.text.includes('No se pudo iniciar sesi')||razor.text.includes('Demasiadas solicitudes')||forgedReply.status!==429||quotaRazor.status!==429||invalidCsrf.status!==400)throw Error('Login partition regression failed');
 fs.mkdirSync('artifacts/performance-login-partitions',{recursive:true});fs.writeFileSync('artifacts/performance-login-partitions/results.json',JSON.stringify(results,null,2));console.log(JSON.stringify(results));
})().catch(e=>{console.error(e.message);process.exit(1)});
