1. Game Design Document 
   1. Proyecto Kheltan —Ivan Sánchez Cuerva
      1. 

   2. 1. Ficha de Producto
Nombre comercial
	Kheltan
	Estudio desarrollador
	Ivan Sanchez Cuerva (LybonAsociados Studios)
	Plataformas objetivo
	Android (SDK 23–34), PC/Standalone
	Orientación de pantalla
	Landscape exclusivo horizontal
	Género
	Juego de Cartas Estratégico por Turnos, Multijugador en Línea
	Versión actual
	0.1.0
	Motor
	Unity 6.2
	

































   3. 2. Concepto y Visión del Producto
Kheltan es un videojuego de cartas 2.5D estratégico por turnos que integra el paradigma clásico de juegos de bazas con predicción en un entorno multijugador en tiempo real sustentado sobre la infraestructura de Unity Gaming Services (UGS).
El núcleo de la propuesta de valor reside en la tensión mental entre la predicción y la ejecución: el jugador no solo debe ganar bazas, sino ganar exactamente el número que declaró en la fase de apuesta. Este mecanismo penaliza tanto el exceso como el defecto de rendimiento, lo que eleva la cota de habilidad percibida y genera bucles de retención intrínsecos.
El producto opera bajo un modelo de distribución Freemium con economía dual (SoftCoins + Trofeos de clasificación), anuncios intersticiales y recompensados, e integración de compras en aplicación (IAP) a través de Unity IAP.


   4.    5. 3. Modos de Juego
El sistema de selección de modo de juego está controlado por la clase MenuManager (máquina de estados de paneles de UI) en coordinación con GameConfig (clase estática de configuración global) y SessionNetworkManager (gestión de sesiones UGS). Se identifican tres modos funcionalmente distintos:  










      2. 3.1. Modo Práctica (Practice)
El jugador humano afronta una partida en local contra bots controlados por AIController. No requiere conexión a internet sostenida durante la partida (el servidor es el mismo dispositivo, en rol de Host local). Los parámetros configurables desde la interfaz son:
* Número de jugadores totales: Entre 2 y 6 (1 humano + hasta 5 bots).
* Tiempo de turno: 5, 10, 15 o 20 segundos.
* Dificultad de la IA: 7 niveles (Ultra Fácil, Fácil, Normal, Difícil, Muy Difícil, Experto, Imposible).
* Premio monetario: Calculado dinámicamente según los parámetros seleccionados (base 50 monedas + bonificaciones por número de jugadores, velocidad de turno y dificultad de la IA, hasta 750 monedas de bonus por nivel "Imposible").
No existe cuota de entrada (currentFee = 0). La partida no penaliza con trofeos.





  




      3.       4.       5. 

      6.       7. 3.2. Modo Privado (Private)
El anfitrión (Host) crea una sala de juego privada a través de SessionNetworkManager.CrearSalaPrivada(), que utiliza el servicio UGS MultiplayerService con Relay Network. Se genera un código alfanumérico de sala que el Host comparte con sus invitados.
Los clientes ingresan mediante SessionNetworkManager.RealizarUnionDefinitiva() previa previsualización de los metadatos de la sala (tarifa de entrada y premio) a través de PrevisualizarSalaExterna(), que consulta datos de CloudSave.Data.Custom.
Parámetros configurables por el anfitrión: número máximo de jugadores (2–6), tiempo de turno, dificultad de los bots que rellenen los asientos vacíos, y cuota de entrada (fee en monedas). El bote total del premio es fee × número de jugadores reales.
El sistema implementa reembolsos automáticos y penalizaciones por abandono del Host, garantizando la integridad de la economía incluso ante desconexiones abruptas.






  

3.3. Modo Público / Matchmaking (Public)
El jugador ingresa a un sistema de emparejamiento automático gestionado por MultiplayerService.MatchmakeSessionAsync() en la cola "PublicQueue" de Unity Gaming Services. El código envía el número de trofeos del jugador como atributo del ticket ({ "trophies", (double)misTrofeos }), El lo utilizará para emparejamiento por rango.
* Cuota fija de entrada: 200 monedas, verificada contra el saldo local antes de lanzar el matchmaking.
* Tiempo máximo de espera (cliente): 60 segundos via CancellationTokenSource, cancelable por el usuario en cualquier momento con el boton de Leave
* Tiempo de expiración del pool: 55 segundos , garantizando que el pool agota su búsqueda antes de que el cliente cancele.
* Capacidad de sala: Hasta 6 jugadores 


Sistema de Relajación de jugadores por tiempo:
El PublicPool aplica un sistema de relajación progresiva del mínimo de jugadores necesario para arrancar la partida. El matchmaker comienza intentando reunir una mesa llena de 6 jugadores y reduce el umbral mínimo conforme avanza el tiempo del ticket más antiguo en espera:
Tiempo de espera
	Jugadores mínimos para arrancar
	Máximo
	0 – 2 seg
	6
	6
	3 – 7 seg
	5
	6
	8 – 12 seg
	4
	6
	13 – 19 seg
	3
	6
	20 seg en adelante
	2
	6
	Sistema de Relajación de trofeos por tiempo:
De forma simultánea, el pool aplica una regla de tipo Difference sobre el atributo trophies que controla la diferencia máxima de trofeos permitida entre el jugador con más y el jugador con menos trofeos de la partida. Dicho umbral se amplía progresivamente si no se encuentra emparejamiento dentro del rango estricto:
Tiempo de espera
	Diferencia máxima de trofeos permitida
	0 – 14 seg
	300 trofeos
	15 – 29 seg
	700 trofeos
	30 seg en adelante
	Sin límite (cualquier rango)
	Ambos sistemas de relajación operan de forma independiente y simultánea. El matchmaker forma la partida en cuanto se satisfacen a la vez el mínimo de jugadores vigente y la regla de trofeos vigente para ese instante. Esto garantiza que el jugador obtenga siempre una partida antes del timeout de 60 segundos, con la mayor calidad de emparejamiento posible dado el tiempo transcurrido.
* Economía de trofeos: Sistema de suma cero dinámica. Los eliminados contribuyen trofeos a un bote que se redistribuye entre los supervivientes de forma proporcional al puesto final 
* Backfill: Desactivado. Si un jugador se desconecta una vez formada la partida, su asiento no se rellena desde la cola. Es eliminado de la partida
      1. 
  























   6. 4. Mecánicas Nucleares del Juego de Cartas
   1. 4.1. La Baraja
El sistema utiliza una baraja estándar de 52 cartas compuesta por 4 palos y 13 rangos, gestionada por la clase estática CardDatabase. Los palos son Corazones, Diamantes, Tréboles y Picas. Los rangos van del As (valor numérico 1) al Rey (valor numérico 13). La baraja se genera completa al inicio de cada ronda si el mazo resulta insuficiente y se baraja mediante el algoritmo de Fisher-Yates.


   2. 4.2. Estructura de la Ronda y Escalada de Cartas
La partida se compone de múltiples rondas. El número de cartas por ronda oscila entre 1 y 5 en un patrón en forma de pirámide invertida: 5 → 4 → 3 → 2 → 1 → 2 → 3 → 4 → 5 → ... Este ciclo es gestionado por InteractionManager.AdvanceRoundSequence().
  

   3. 4.3. Fase de Apuestas 
Al inicio de cada ronda, tras el reparto de cartas orquestado por HandTester.DrawNewHand() , se activa el panel de BettingManager. Cada jugador, en orden desde el jugador que determino como la “Mano” (determinado por sorteo inicial y rotación posterior), declara cuántas bazas predice ganar en esa ronda. Restricción fundamental: el último jugador en apostar no puede declarar un valor que igualaría la suma total de apuestas al número de cartas en juego, garantizando que al menos un jugador falle su predicción en cada ronda.
En partidas con bots, AIController.CalculateAIBet() o CalculateBlindBet() (para rondas de 1 carta) generan la apuesta automáticamente según el nivel de dificultad configurado.




   4. 4.4. Fase de Juego de Cartas 
Dentro de cada ronda, los jugadores turnan en orden circular.
El jugador activo selecciona una carta de su mano y la deposita el centro de la mesa 
La validación de turno es autoritativa en el servidor: Se verifica que el emisor del RPC es el jugador cuyo turno es activo antes de ejecutar la jugada.
Cuando todos los jugadores vivos han jugado una carta, se determina el ganador de la baza por TableZone.CheckWinner():
Fórmula de puntuación de carta:
score = (card.value × 10) + suitValue
Jerarquía de palos: Diamantes (4) > Corazones (3) > Picas (2) > Tréboles (1).
El ganador de la baza acumula un contador de bazas ganadas. Se anuncian visualmente por AnunciarGanadorBazaClientRpc.

   5. 4.5. Resolución de Ronda y Sistema de Vidas
Al agotarse todas las bazas de la ronda, TableZone.ResolverApuestas() compara la apuesta de cada jugador contra sus bazas reales:
   * Acierto: El jugador sobrevive sin penalización.
   * Fallo (exceso o defecto): El jugador pierde 1 vida. Cada jugador parte con 3 vidas (vidas[i] = 3 en la inicialización).
El jugador que agota sus vidas es eliminado. Si queda un único jugador vivo, se declara vencedor de la partida mediante PauseManager.TriggerGameOver()  


4.6. Inteligencia Artificial (AIController)


La IA implementa 7 niveles de dificultad estrictamente diferenciados, cada uno construido incrementalmente sobre el anterior:
Nivel
	Nombre
	Comportamiento principal
	0
	Ultra Fácil
	Apuesta y juega al azar puro
	1
	Fácil
	Lógica base con 30% de error deliberado
	2
	Normal
	Lógica matemática correcta sobre fuerza de carta
	3
	Difícil
	Solo refuerza el valor de una carta cuando ya NO quedan superiores de ese palo en juego ni en su propia mano
	4
	Muy Difícil
	2 vidas del humano, actúa también cuando va primero (no solo como respuesta); apuesta conservadoramente con 1 vida propia
	5
	Experto
	Lector de mazo activo: gana con la carta mínima suficiente (preserva las buenas), pierde con la carta máxima posible sin ganar (no malgasta bazas)
	6
	Imposible
	Lee la mano del humano y sus apuestas; juega psicológicamente según si el humano necesita ganar o perder bazas
	















Detalle de las mecánicas avanzadas:
Nivel 3 — Memoria corregida: la carta solo es "líder de palo" cuando todas las superiores han sido jugadas o están en su propia mano. 


Nivel 4 — Anti-player 


   * Umbral de activación: if (humanLives > 2) return null — el modo sanguinario se activa cuando el humano tiene 2 vidas o menos.
   * Actúa en primer turno: Si table.Count == 0 (el bot tira primero), lanza su carta más alta tanto si el humano necesita perder bazas (para forzarle a ganar) como si las necesita ganar (para robárselas).
   * Apuesta propia con 1 vida: Aplica FloorToInt(estimatedWins) en lugar de RoundToInt, siendo más conservador. En la apuesta ciega, reduce la probabilidad de apostar 1 en 0.30 puntos.
   * Nivel 5 — Deck Reader activo (ChooseCardDeckReader): Construye los conjuntos myHandIds, playedIds y deckIds. Para ganar, selecciona el mínimo ganador de la lista ordenada descendente. Para perder, selecciona el máximo perdedor.


Nivel 6 — : Lee InteractionManager.Instance.bazasGanadas[humanSeat] y apuestas[humanSeat] para determinar el estado del humano. Tabla de decisiones cuando el bot va primero (table.Count == 0):
Estado del humano
	Acción del bot
	Necesita ganar bazas
	Tira un cebo: la carta más alta que el humano aún puede superar 
	No necesita ganar bazas
	Tira una trampa: carta imbatible para forzarle a ganar y que falle su apuesta 
	Bot tira primero y necesita ganar
	Tira la carta que el humano no puede superar 
	

Los bots de nivel 3+ mantienen la lista _cardsPlayedThisRound, reseteada en ResetRoundMemory() al inicio de cada ronda y poblada mediante RegisterPlayedCards() tras resolver cada baza.
   7.    8. 5. Flujos de Pantalla e Interfaz de Usuario
   6. 5.1. Escena: MainMenu
Gestionada por MenuManager como máquina de estados de paneles. Los paneles identificados son:
   * Hub Central: Punto de partida con acceso a todos los modos y la tienda.
   * Panel de Bienvenida : Se activa en primer arranque o cuando el usuario no tiene nickname configurado. Gestiona la creación de alias y vinculación de cuenta.
   * Panel de Perfil: Muestra estadísticas e historial de partidas. Permite edición de avatar (desde galería nativa via UnityNativeGallery) y vinculación de cuenta user/password.
   * Panel de Tienda (Shop): Catálogo de paquetes de monedas e ítem "Sin Publicidad".
   * Panel de Práctica: Selectores de configuración y botón de inicio.
   * Paneles de Modo Público: Pantalla de matchmaking con indicador de búsqueda.
   * Paneles de Modo Privado: Elección (crear/unirse), lobby de anfitrión con código de sala, lobby de cliente.
   * Popup Global: Ventana modal de información y errores con enrutamiento a la tienda si el error es de saldo insuficiente.
   * Panel de Settings: Permite apagar o encender la música y los sfx del juego.
   * TopBar: Monedas y Trofeos del jugador y acceso a la tienda y al Perfil.

5.2. Escena: MainGame
Contiene la mesa de juego con los siguientes elementos funcionales:
      * Zona de manos de jugadores (HandAreas, instanciadas dinámicamente por TableManagerLayout).
      * Zona de mesa central (TableZone): área de drop de cartas.
      * Panel de Apuestas (BettingManager.panelRoot): aparece al inicio de cada ronda.
      * Panel de Pausa/Game Over (PauseManager.pausePanel): activado por tecla Escape, boton de pausa o fin de partida.
      * Barra informativa (InteractionManager.infoLineText): mensajes contextuales de turno disponibles en una television a la derecha de la escena
      * TableTrackerUI: Historial visual de cartas sobre la mesa.
      * TimerUI: Cuenta regresiva del turno del jugador humano 





6. Sistema de Perfil y Progresión del Jugador
El sistema de perfil está centralizado en ProfileManager (Singleton persistente DontDestroyOnLoad) y estructura los datos del jugador en cuatro entidades NoSQL persistidas en Unity Cloud Save:
         * PlayerProfile: Identidad del jugador (nickname, avatar, fecha de registro, skins).
         * Wallet: Economía (monedas, trofeos, estado NoAds).
         * PlayerStats: Estadísticas segregadas por modo de juego (partidas jugadas, victorias, racha máxima, dificultad más alta superada).
         * MatchHistory: Historial cronológico de hasta 50 partidas con fecha, modo, posición, jugadores y variación económica.
La autenticación inicial es anónima automática (AuthenticationService.SignInAnonymouslyAsync()). El jugador puede posteriormente vincular su cuenta anónima a credenciales Email/Password mediante ProfileManager.VincularCuenta(), garantizando persistencia cross-dispositivo.
  
  

  
  



         9. 7. Economía de Juego y Sistema de Monetización
         1. 7.1. Monedas (Soft Currency)
Divisa principal del juego. Obtenida mediante:
         * Premio de partida: Variable según modo y resultado.
         * Anuncio recompensado: +100 monedas por visualización completa (Unity Ads).
         * Compra directa (IAP): 6 paquetes disponibles en la tienda: 10.000 / 18.000 / 40.000 / 87.000 / 250.000 / 750.000 monedas.
Consumida mediante:
         * Cuota de entrada (Fee): En partidas privadas (configurable) y públicas (200 monedas fijas).
         * Inicio de partida: El fee se descuenta al arrancar la escena de juego (TopBarUI.QueuePendingDelta).


  


  

7.2. Trofeos (Ranking Currency)
Divisa de clasificación competitiva. Exclusiva del Modo Público. Opera bajo un sistema de suma cero dinámica: los trofeos que pierden los eliminados forman un bote que se redistribuye entre los supervivientes al finalizar la partida (GameConfig.trophyBote).
         2. 7.3. Anuncios (Unity Ads v4.16.4)
         * Intersticial: Se muestra al finalizar la partida. La frecuencia es controlada por el sistema de Test A/B (RemoteConfigManager.adStrategy):
         * Estrategia "aggressive" (Grupo A): Anuncio siempre, independientemente del resultado.
         * Estrategia "punitive" (Grupo B): Anuncio únicamente si el jugador ha perdido.
         * Recompensado: Disponible bajo demanda desde la tienda. Otorga 100 monedas al completarse.
         * Exención de publicidad: Los jugadores que adquieran el ítem "Sin Publicidad" (NoAdsOwned) quedan exentos de los anuncios intersticiales. El estado se sincroniza en CloudSave.


7.4. Compras en Aplicación (Unity IAP v5.0.4)
Los identificadores de producto registrados en el ShopController son:
ID de Producto
	Tipo
	Entrega
	com.kheltan.coins_10k
	Consumible
	10.000 monedas
	com.kheltan.coins_18k
	Consumible
	18.000 monedas
	com.kheltan.coins_40k
	Consumible
	40.000 monedas
	com.kheltan.coins_87k
	Consumible
	87.000 monedas
	com.kheltan.coins_250k
	Consumible
	250.000 monedas
	com.kheltan.coins_750k
	Consumible
	750.000 monedas
	com.kheltan.remove_ads
	No Consumible
	Eliminación permanente de publicidad intersticial
	La tienda opera en modo simulación para la evaluación. El flujo de compra real está codificado en ProcessPurchase() y se activaría registrando los 7 productos en Google Play Console con los mismos IDs que ya están en el código .


         3. 7.5. Test A/B de Estrategia Publicitaria
El sistema RemoteConfigManager descarga la clave ad_strategy del servicio Unity Remote Config tras la autenticación. El Dashboard de UGS asigna automáticamente al jugador al Grupo A o Grupo B. Esto constituye el mecanismo de A/B Testing implementado.





         10. 8. Integridad Económica y Sistemas Anti-Trampa
         11. El sistema implementa múltiples capas de protección de la integridad económica
         12. Anti-RageQuit offline: Al iniciar cualquier partida pública, se registra la flag PartidaEnCurso = 1 en PlayerPrefs junto con la penalización de trofeos máxima correspondiente a la posición actual del jugador. PauseManager.UpdateAntiRageQuitPenalty() actualiza esta penalización en cada cambio de estado de vidas, de modo que siempre refleja el castigo correspondiente al puesto en curso. Si la aplicación se cierra de forma abrupta (Alt+F4, deslizamiento en móvil, corte de proceso), la penalización de trofeos se aplica automáticamente la próxima vez que el jugador cargue el menú, dentro de TopBarUI.CargarEconomiaNube().
         13. Cola offline de economía: Los cambios económicos originados en la escena de juego (donde TopBarUI no existe) se encolan de forma segura en PlayerPrefs mediante TopBarUI.QueuePendingDelta(trofeoDelta, monedaDelta) y se aplican al regresar al menú principal. Las monedas están protegidas contra valores negativos en toda la cadena
         14. Autoridad del servidor: Toda validación de jugadas de cartas y de apuestas pasa por el servidor antes de ejecutarse en los clientes, impidiendo manipulaciones locales del estado de juego.
________________
         15.          16. Comportamiento por abandono   Partidas Públicas
Quién abandona
	Dinero
	Trofeos
	Extra
	Cliente voluntariamente
	❌ Fee no reembolsado — permanece en el bote del ganador
	❌ Pierde trofeos según posición (trophyDeltaByRank), nunca gana por abandonar
		Host voluntariamente
	❌ Fee no reembolsado
	❌ Pierde trofeos según posición
	🎥 Forzado a ver un anuncio
	Clientes cuando el Host cae
	✅ Fee reembolsado íntegramente
	✅ Reciben parte proporcional del bote acumulado + trofeos perdidos por el host (mínimo 20) + bonus por posición (+30 si eran el único superviviente; +10 si quedaban dos)
		Host que permanece hasta el final
	✅ +50 monedas de incentivo por mantener el servidor activo
	✅ +5 trofeos de bonus
	Aunque haya perdido todas sus vidas
	         17.          18. Comportamiento por abandono   Partidas Privadas
Quién abandona
	Consecuencia económica
	Cliente voluntariamente
	Pierde su fee de entrada (ya deducido al unirse). Sin penalización adicional.
	Host voluntariamente
	Pierde su fee de entrada más una penalización fija adicional de 1 × fee — total fee × 2 independientemente del número de jugadores. El pool resultante (2 × fee) se distribuye a partes iguales entre los clientes 
	         19. La penalización del host en privada está fijada a fee × 2 para evitar que abandonar una sala grande suponga una pérdida económica desproporcionada.
Las partidas de practica no tienen ningun castigo por abandono pues la entrada es gratis.


9. Dirección de Arte y Estilo Visual
9.1. Concepto Estético y Atmósfera
Kheltan apuesta por una estética de juego de cartas clásico europeo con alma de taberna. La experiencia visual se bifurca en dos registros complementarios: el menú principal, que representa la elegancia de un tapete de casino (fondo de fieltro verde oscuro con motivos de palos de baraja en relieve) Ademas de contar con un hilo musical con un tono desenfadado y que acompaña muy bien la estetica de casino del menu.

La escena de juego, sumerge al jugador en el calor de una taberna ilustrada de ambientación mediterránea, con iluminación cálida, estanterías repletas de botellas y una mesa ovalada de madera como centro de acción. Este cuenta con otro hilo musical distinto, en este caso un Jazz suave tipico de casino que refuerza la estetica de la escena y le da un toque de tranquilidad.

Los efectos de sonido son reducidos pero muy bien integados, desde sonidos (distintos dependiendo de la importancia del boton) en los botones hasta las cartas moviendose en la mesa y un ligero sonido que aparece cuando pierdes una vida mejorando asi la experiencia del usuario.
.
El conjunto transmite que es un juego de cartas tradicional.
La paleta cromática del interfaz refuerza esta dualidad: verdes profundos para los fondos, dorados cálidos y cobres para los elementos interactivos y monedas, y rojos de alerta para las vidas perdidas.


Paleta cromática principal:
Token
	Hex
	Uso
	Fondo Oscuro
	#176042
	Fondos de menú, tapete principal
	Fondo Mesa
	#664729
	Contraste en zona de juego
	Primario
	#F4C35B
	Botones activos, iconos de monedas
	Acento
	#D87A29
	Acento decorativo, bordes de carta
	Alerta
	#E04630
	Vidas perdidas, errores
	

9.2. El Prefab de Carta y su Potencial de Skins
El elemento visual central del juego es el prefab de carta, diseñado como una unidad completamente autocontenida y parametrizable. 
Cada carta está compuesta únicamente por 4 sprites de palo (corazones, picas, diamantes y tréboles) combinados con texto numérico renderizado en tiempo de ejecución, lo que permite representar las 52 cartas de la baraja con un footprint de assets mínimo. El propio codigo colorea los suits de rojo o negro.
Esta arquitectura hace que el sistema de skins sea trivialmente implementable: sustituir los 4 sprites de palo y el sprite de reverso de carta equivale a un cambio de baraja completo. Futuros mazos temáticos (medieval, noir, navideño…) requieren únicamente ese conjunto de 5 assets sin ningún cambio de código.

            
  
  
  
  





9.3. Tipografía
La fuente principal del proyecto es Belwe Bold, visible en el logotipo "Kheltan", en los títulos de modo de juego, botones, cartas, avisos y en los textos de mayor jerarquía visual.


9.4. Sistema de UI Procedural y Atmosfera
Los botones, píldoras de marcador y paneles del interfaz son generados de forma procedural por la clase UIThemeManager ([ExecuteAlways]), que construye sprites píxel a píxel con efectos de iluminación inset, reflejo y antialiasing manual.

La perspectiva 2.5D de la escena de juego de una cámara inclinada sobre una mesa 3D con cartas 2D con una rotacion e inclinacion calculadas por TableManagerLayout y el Fanner, esto refuerza esta sensación de inmersión sin necesidad de un motor de renderizado 3D completo, manteniendo el rendimiento óptimo en móvil. 


9.5. Orientación y Resolución
El juego opera en Landscape exclusivo. La resolución base de diseño es 1920×1080 para PC/Editor y para Android.





            20. 10. Público Objetivo y Análisis de Mercado
            1. 10.1. Perfil Demográfico Principal
La complejidad mecánica (apuestas de bazas, lectura de palos con jerarquía, gestión de economía propia) establece una franja de jugadores semi-hardcore con interés en juegos de cartas estratégicos.:
            * Edad: 18–40 años.
            * Género: Mixto, con ligera inclinación masculina dada la mecánica competitiva de ranking.
            * Intereses: Juegos de cartas (póker, bridge, truco, mus), estrategia por turnos, competición online.
            * Plataforma preferente: Móvil (Android) como plataforma de consumo primario.










10.2. Competencia Directa (Estudio de Mercado)
El segmento de juegos de cartas de bazas con predicción es un nicho con baja saturación en móvil. Los competidores directos identificados son:
Competidor
	Similitud
	Diferenciación de Kheltan
	Wizard
	Mecánica de predicción de bazas idéntica
	Multijugador en tiempo real con economía integrada
	Oh Hell! online
	Mismo género de juego
	Identidad de marca original + monetización F2P
	Hearthstone
	Juego de cartas digital con economía
	Accesibilidad de reglas (baraja estándar, sin colección)
	

La propuesta de valor diferencial de Kheltan es la combinación de mecánicas familiares de baraja estándar (baja barrera de entrada) con profundidad estratégica de apuestas y multijugador en tiempo real con economía competitiva (trofeos y ranking), en un segmento escasamente explotado en el mercado móvil español e hispanoamericano.


            21. 11. Accesibilidad y Experiencia de Usuario
            * Modo espectador: Tras la eliminación, el jugador puede continuar observando la partida si quedan otros humanos activos (PauseManager.SpectateGame()).
            * Temporizador de turno visible: El tiempo restante se sincroniza por red (NetworkVariable<float> turnEndTime) y se muestra en TimerUI, con auto-acción (apuesta forzada o turno saltado) al agotarse.
            * Feedback de estado: La línea de información (infoLineText) proporciona mensajes contextuales permanentes sobre el estado del turno, resultados de bazas y vidas perdidas.
            * RectTransform anclados: La UI está construida sobre el sistema de anclajes de Unity UGUI (v2.0.0), garantizando adaptabilidad a distintas resoluciones de pantalla sin deformación de elementos.
            * Popup de error de saldo: Cuando el jugador no dispone de fondos suficientes para una acción, el sistema redirige automáticamente a la tienda desde el popup informativo.
            * Panel de Settings: Permite desactivar los sonidos del juego, en un futuro se planeará permitir cambiar tamaños de letras y contraste.