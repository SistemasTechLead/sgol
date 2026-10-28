# Comprobaciones de seguridad

`scripts/comprobar-seguridad.ps1` crea servicios separados en puertos 5549/8339/3319,
credenciales aleatorias protegidas con DPAPI y bases `sgol_task_checks_security_*`.
No utiliza ni borra volúmenes de la simulación. Los servicios y la evidencia quedan
para diagnóstico; detenerlos con el compose de esta carpeta, sin `down -v`.

El ejecutable arranca **Sgol.Web.Program real en otro proceso**, con EF, cookies,
Razor, API, PostgreSQL y almacenamiento reales. Los fallos de aserción devuelven
código distinto de cero. Los resultados y logs se guardan en `artifacts/security-audit/implementation/`.
Los controles de servicios con reloj y fallos inducidos complementan HTTP.

No imprimir configuración de proceso, cookies ni credenciales. Las pruebas tienen
presupuesto finito; nunca apuntarlas al puerto/base habitual.
