// Visor local: solo sirve el HTML generado, sin listar carpetas ni otros archivos.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const html = path.resolve(__dirname, '../.local/graphify/visualizacion/graph.html');
const server = http.createServer((req, res) => {
  if (req.headers.host !== '127.0.0.1:7389') { res.writeHead(403).end(); return; }
  if (!['GET', 'HEAD'].includes(req.method)) { res.writeHead(405).end(); return; }
  res.setHeader('Cache-Control', 'no-store');
  if (req.url === '/health') {
    res.setHeader('Content-Type', 'text/plain');
    res.end('sgol-graphify-viewer-v1');
  } else if (req.url === '/graph.html' || req.url === '/') {
    fs.readFile(html, (error, data) => {
      if (error) { res.writeHead(503).end('Generar primero el grafo de la boveda.'); return; }
      res.setHeader('Content-Type', 'text/html; charset=utf-8');
      res.end(req.method === 'HEAD' ? undefined : data);
    });
  } else { res.writeHead(404).end(); }
});
server.listen(7389, '127.0.0.1');
server.on('error', error => { console.error(error.message); process.exit(1); });
