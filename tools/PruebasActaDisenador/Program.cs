// ═══════════════════════════════════════════════════════════════════════════
// Pruebas funcionales del diseñador de plantillas de acta.
//
//   dotnet run --project tools/PruebasActaDisenador
//
// Recorre el camino completo con la base de datos real: valida una definición
// como la que envía el navegador, la guarda, la vuelve a resolver, renderiza el
// pliego, emite un acta con tres firmas y comprueba que se lee de vuelta igual.
// Al terminar borra todo lo que creó.
// ═══════════════════════════════════════════════════════════════════════════

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.EspacioCorporativo;

var fallos = 0;
var pruebas = 0;

void Verificar(string nombre, bool condicion, string? detalle = null)
{
    pruebas++;
    if (condicion)
    {
        Console.WriteLine($"  OK    {nombre}");
        return;
    }

    fallos++;
    Console.WriteLine($"  FALLA {nombre}{(detalle is null ? "" : $"  ->  {detalle}")}");
}

void Seccion(string titulo)
{
    Console.WriteLine();
    Console.WriteLine($"== {titulo} ==");
}

// ── Conexión ────────────────────────────────────────────────────────────────

var rutaSecretos = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Microsoft", "UserSecrets", "Nexa-Local-Secrets", "secrets.json");

if (!File.Exists(rutaSecretos))
{
    Console.WriteLine($"No se encontro {rutaSecretos}");
    return 1;
}

using var secretos = JsonDocument.Parse(File.ReadAllText(rutaSecretos));
var cadena = secretos.RootElement.GetProperty("ConnectionStrings:DefaultConnection").GetString();

var opciones = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(cadena)
    .Options;

await using var db = new ApplicationDbContext(opciones);

var firmante = new EspacioActaRenderer.DatosFirmante("Emmanuel Estrada", "1000000001", "Lider de Tecnologia");
var fecha = new DateTime(2026, 8, 25, 14, 30, 0, DateTimeKind.Unspecified);

// ── 1. El validador rechaza definiciones incompletas ────────────────────────

Seccion("1. Validacion de definiciones invalidas");

var sinCampos = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta vacia",
    TituloActa = "ACTA VACIA"
});
Verificar("Sin campos no se guarda", !sinCampos.EsValida);
Verificar(
    "Explica que faltan datos, sin jerga",
    sinCampos.Errores.Any(x => x.Contains("dato", StringComparison.OrdinalIgnoreCase)),
    string.Join(" | ", sinCampos.Errores));

var sinNombre = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    TituloActa = "ACTA SIN NOMBRE",
    Campos = [new() { Clave = "n", Etiqueta = "Nombre", Tipo = "Texto" }],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Hola" }],
    Firmas = [new() { Rotulo = "Recibe", Origen = "EnVivo", CampoNombre = "n" }],
    CampoNombre = "n"
});
Verificar("Sin nombre de plantilla no se guarda", !sinNombre.EsValida);

var marcadorRoto = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta con hueco",
    TituloActa = "ACTA",
    CampoNombre = "nombre",
    Campos = [new() { Clave = "nombre", Etiqueta = "Nombre", Tipo = "Texto" }],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Hola {{nombre}} y {{no_existe}}." }],
    Firmas = [new() { Rotulo = "Recibe", Origen = "EnVivo", CampoNombre = "nombre" }]
});
Verificar("Un dato borrado que sigue en el texto detiene la publicacion", !marcadorRoto.EsValida);
// El aviso no imprime la clave interna a proposito: en el editor ese dato se ve
// tachado en rojo dentro del texto, que es donde hay que arreglarlo.
Verificar(
    "Y lo explica sin mostrar codigos",
    marcadorRoto.Errores.Any(x =>
        x.Contains("ya no existen", StringComparison.OrdinalIgnoreCase)
        && !x.Contains("{{", StringComparison.Ordinal)),
    string.Join(" | ", marcadorRoto.Errores));

var firmaSinNombre = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta",
    TituloActa = "ACTA",
    CampoNombre = "nombre",
    Campos = [new() { Clave = "nombre", Etiqueta = "Nombre", Tipo = "Texto" }],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Hola {{nombre}}." }],
    Firmas = [new() { Rotulo = "Testigo", Origen = "EnVivo" }]
});
Verificar("Una firma sin nombre ni campo se rechaza", !firmaSinNombre.EsValida);

var soloEmisor = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta",
    TituloActa = "ACTA",
    CampoNombre = "nombre",
    Campos = [new() { Clave = "nombre", Etiqueta = "Nombre", Tipo = "Texto" }],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Hola {{nombre}}." }],
    Firmas = [new() { Rotulo = "Entrega", Origen = "Emisor" }]
});
Verificar("Un acta que solo firma el emisor se rechaza", !soloEmisor.EsValida);

var listaSinOpciones = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta",
    TituloActa = "ACTA",
    CampoNombre = "nombre",
    Campos =
    [
        new() { Clave = "nombre", Etiqueta = "Nombre", Tipo = "Texto" },
        new() { Clave = "modalidad", Etiqueta = "Modalidad", Tipo = "Seleccion" }
    ],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Hola {{nombre}}." }],
    Firmas = [new() { Rotulo = "Recibe", Origen = "EnVivo", CampoNombre = "nombre" }]
});
Verificar("Una lista sin opciones se rechaza", !listaSinOpciones.EsValida);

// El enlace del correo es una comodidad, no un requisito: si apunta a un dato que
// no es correo se descarta en silencio en vez de frenar a quien esta armando el acta.
var correoMalEnlazado = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta",
    TituloActa = "ACTA",
    CampoNombre = "nombre",
    CampoCorreo = "nombre",
    Campos = [new() { Clave = "nombre", Etiqueta = "Nombre", Tipo = "Texto" }],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Hola {{nombre}}." }],
    Firmas =
    [
        new() { Rotulo = "Entrega", Origen = "Emisor" },
        new() { Rotulo = "Recibe", Origen = "EnVivo", CampoNombre = "nombre" }
    ]
});
Verificar("Un correo mal enlazado no invalida la plantilla", correoMalEnlazado.EsValida);
Verificar("Simplemente queda sin envio de copia", correoMalEnlazado.Plantilla?.CampoCorreo is null);

// ── 2. Una definición completa se normaliza ─────────────────────────────────

Seccion("2. Normalizacion de una plantilla completa");

var definicion = new EspacioActaDefinicionDto
{
    Nombre = "Acta de compromiso de dotacion",
    Descripcion = "Entrega de uniformes y elementos de proteccion personal.",
    Icono = "bi-box-seam",
    TituloActa = "ACTA DE COMPROMISO DE DOTACION",
    NumerarTitulos = true,
    CampoNombre = "nombre",
    CampoDocumento = "documento",
    CampoCorreo = "correo",
    Campos =
    [
        new() { Clave = "nombre", Etiqueta = "Nombre completo", Tipo = "Texto" },
        new() { Clave = "documento", Etiqueta = "Documento", Tipo = "Documento" },
        new() { Clave = "correo", Etiqueta = "Correo", Tipo = "Correo", VisibleEnActa = false },
        new() { Clave = "fecha_entrega", Etiqueta = "Fecha de entrega", Tipo = "Fecha" },
        new() { Clave = "valor", Etiqueta = "Valor de la dotacion", Tipo = "Moneda" },
        new() { Clave = "prendas", Etiqueta = "Prendas entregadas", Tipo = "Numero" },
        new() { Clave = "hora", Etiqueta = "Hora", Tipo = "Hora" },
        new() { Clave = "acepta", Etiqueta = "Acepta el reglamento", Tipo = "Casilla" },
        new()
        {
            Clave = "sede",
            Etiqueta = "Sede",
            Tipo = "Seleccion",
            Opciones = [new() { Valor = "Medellin", Etiqueta = "Medellin" }, new() { Valor = "Bogota", Etiqueta = "Bogota" }]
        },
        // Sin clave: el normalizador debe generarla a partir del rotulo.
        new() { Etiqueta = "Observaciones del jefe", Tipo = "TextoLargo", Requerido = false }
    ],
    Bloques =
    [
        new()
        {
            Tipo = "Parrafo",
            Texto = "En {{__ciudad}}, a {{__fecha_completa}}, **{{__firmante_nombre}}** hace entrega a "
                    + "**{{nombre}}** (C.C. {{documento}}) de la dotacion descrita, el {{fecha_entrega}} "
                    + "a las {{hora}} en la sede {{sede}}."
        },
        new() { Tipo = "Titulo", Texto = "Detalle de la entrega" },
        new() { Tipo = "Datos", Campos = ["prendas", "valor", "fecha_entrega"] },
        new() { Tipo = "Titulo", Texto = "Compromisos" },
        new() { Tipo = "Lista", Texto = "Usar la dotacion completa\nReportar deterioro\nDevolverla al retirarse" },
        new() { Tipo = "Nota", Texto = "Acepta el reglamento: {{acepta}}" },
        new() { Tipo = "Separador" },
        // Texto hostil: debe salir escapado, nunca como marcado.
        new() { Tipo = "Parrafo", Texto = "<script>alert('x')</script> y un & suelto." }
    ],
    Firmas =
    [
        new() { Clave = "emisor", Rotulo = "Entrega", Origen = "Emisor" },
        new() { Clave = "recibe", Rotulo = "Recibe", Origen = "EnVivo", CampoNombre = "nombre", CampoDocumento = "documento" },
        new() { Clave = "testigo", Rotulo = "Testigo", Origen = "EnVivo", NombreFijo = "Jefe de Talento Humano", Requerida = false }
    ]
};

var normalizada = EspacioActaDisenador.Normalizar(definicion);
Verificar("La definicion completa es valida", normalizada.EsValida, string.Join(" | ", normalizada.Errores));

if (!normalizada.EsValida)
{
    Console.WriteLine();
    Console.WriteLine($"RESULTADO: {pruebas - fallos}/{pruebas} pruebas OK, {fallos} fallas.");
    return 1;
}

var plantilla = normalizada.Plantilla!;
Verificar("Se generaron 10 campos", plantilla.Campos.Count == 10, $"fueron {plantilla.Campos.Count}");
Verificar(
    "El campo sin clave recibio una generada",
    plantilla.Campos[9].Clave == "observaciones_del_jefe",
    plantilla.Campos[9].Clave);
Verificar("Se conservaron las 3 firmas", plantilla.FirmasEfectivas.Count == 3);
Verificar("El codigo lleva el prefijo de plantilla propia", plantilla.Codigo.StartsWith("PZ_", StringComparison.Ordinal), plantilla.Codigo);
Verificar("El bloque Separador sobrevive sin texto", plantilla.Bloques.Any(x => x.Tipo == EspacioActaTipoBloque.Separador));

// ── 3. Renderizado ──────────────────────────────────────────────────────────

Seccion("3. Renderizado del pliego");

var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
{
    ["nombre"] = "Yirley Yulieth Yanes",
    ["documento"] = "1077463495",
    ["correo"] = "yyanes@especialistasencasa.com",
    ["fecha_entrega"] = "2026-08-12",
    ["valor"] = "1250000",
    ["prendas"] = "4",
    ["hora"] = "14:30",
    ["acepta"] = "Si",
    ["sede"] = "Medellin",
    ["observaciones_del_jefe"] = "Primera dotacion del ano."
};

var html = EspacioActaRenderer.Render(plantilla, valores, firmante, fecha);

Verificar("El nombre entra en el documento", html.Contains("Yirley Yulieth Yanes", StringComparison.Ordinal));
Verificar("La fecha se imprime en letras", html.Contains("12 de agosto de 2026", StringComparison.Ordinal), Recorte(html, "agosto"));
Verificar("La hora se imprime en formato de reloj", html.Contains("02:30", StringComparison.Ordinal), Recorte(html, "02:"));
Verificar("El dinero se imprime con separadores", html.Contains("1.250.000", StringComparison.Ordinal), Recorte(html, "250"));
Verificar("La casilla marcada se imprime como Si", html.Contains("acta-nota", StringComparison.Ordinal) && html.Contains("Sí", StringComparison.Ordinal));
Verificar("Los asteriscos dobles producen negrita", html.Contains("<strong>Yirley Yulieth Yanes</strong>", StringComparison.Ordinal));
Verificar("Los titulos se numeran", html.Contains("<h2>1. Detalle de la entrega</h2>", StringComparison.Ordinal), Recorte(html, "<h2>"));
Verificar("El segundo titulo sigue la numeracion", html.Contains("<h2>2. Compromisos</h2>", StringComparison.Ordinal));
Verificar("La lista genera tres vinetas", ContarOcurrencias(html, "<li>") >= 3);
Verificar("El cuadro de datos imprime la etiqueta", html.Contains("<strong>Valor de la dotacion:</strong>", StringComparison.Ordinal), Recorte(html, "Valor de"));
Verificar("El separador se dibuja", html.Contains("<hr", StringComparison.Ordinal));

Verificar(
    "El texto hostil sale escapado",
    !html.Contains("<script>", StringComparison.OrdinalIgnoreCase)
    && html.Contains("&lt;script&gt;", StringComparison.Ordinal),
    Recorte(html, "script"));

Verificar(
    "Un & suelto queda codificado",
    html.Contains("&amp;", StringComparison.Ordinal),
    Recorte(html, "suelto"));

// Un valor hostil dentro de un campo tampoco puede inyectar marcado.
var valoresHostiles = new Dictionary<string, string?>(valores)
{
    ["nombre"] = "<img src=x onerror=alert(1)>"
};
var htmlHostil = EspacioActaRenderer.Render(plantilla, valoresHostiles, firmante, fecha);
Verificar(
    "Un valor hostil del formulario sale escapado",
    !htmlHostil.Contains("<img", StringComparison.OrdinalIgnoreCase)
    && htmlHostil.Contains("&lt;img", StringComparison.Ordinal));

// El resaltado solo aparece cuando se pide.
var htmlResaltado = EspacioActaRenderer.Render(plantilla, valores, firmante, fecha, resaltarVariables: true);
Verificar("La vista previa resalta las variables", htmlResaltado.Contains("acta-var", StringComparison.Ordinal));
Verificar("El acta firmada no lleva resaltado", !html.Contains("acta-var", StringComparison.Ordinal));

// Sin numeracion los titulos salen limpios.
var sinNumerar = plantilla with { NumerarTitulos = false };
var htmlSinNumero = EspacioActaRenderer.Render(sinNumerar, valores, firmante, fecha);
Verificar("Sin numerar, el titulo sale sin prefijo", htmlSinNumero.Contains("<h2>Detalle de la entrega</h2>", StringComparison.Ordinal));

// La plantilla de fabrica debe seguir funcionando igual que antes.
var deFabrica = EspacioActaPlantillas.Obtener(EspacioActaPlantillas.CodigoAccesosTecnologicos)!;
var htmlFabrica = EspacioActaRenderer.Render(
    deFabrica,
    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["tratamiento"] = "a la señora",
        ["nombre_recibe"] = "Yirley Yanes",
        ["documento_recibe"] = "1077463495",
        ["software"] = "Manager",
        ["usuario"] = "YYANES",
        ["contrasena"] = "Clave123*",
        ["urls"] = "https://manager.especialistasencasa.com"
    },
    firmante,
    fecha);
Verificar("La plantilla de fabrica sigue renderizando", htmlFabrica.Contains("Yirley Yanes", StringComparison.Ordinal));
Verificar("La plantilla de fabrica enlaza las URLs", htmlFabrica.Contains("<a href=", StringComparison.Ordinal));
Verificar("La plantilla de fabrica tiene 2 firmas por defecto", deFabrica.FirmasEfectivas.Count == 2);

// ── 4. Guardado y resolución desde la base ──────────────────────────────────

Seccion("4. Guardado, lectura y resolucion");

var entidad = new EspacioActaPlantillaPersonalizada
{
    Codigo = plantilla.Codigo,
    CreadaPorUserId = null,
    CreadaPorNombre = "Prueba automatizada",
    CreadaAtUtc = DateTime.UtcNow,
    Version = 1
};

EspacioActaDisenador.Volcar(plantilla, entidad);
db.EspacioActaPlantillas.Add(entidad);
await db.SaveChangesAsync();

Verificar("La plantilla quedo guardada con id", entidad.Id > 0);

db.ChangeTracker.Clear();

var leida = await db.EspacioActaPlantillas.AsNoTracking().FirstAsync(x => x.Id == entidad.Id);
var plantillaLeida = EspacioActaDisenador.ADominio(leida);

Verificar("Al releerla conserva los campos", plantillaLeida.Campos.Count == plantilla.Campos.Count);
Verificar("Al releerla conserva los bloques", plantillaLeida.Bloques.Count == plantilla.Bloques.Count);
Verificar("Al releerla conserva las firmas", plantillaLeida.Firmas.Count == 3);
Verificar("Al releerla conserva los tipos de campo", plantillaLeida.Campos[4].Tipo == EspacioActaTipoCampo.Moneda, plantillaLeida.Campos[4].Tipo.ToString());
Verificar("Al releerla conserva el origen de la firma del emisor", plantillaLeida.Firmas[0].Origen == EspacioActaFirmaOrigen.Emisor);
Verificar("Al releerla reconoce que es personalizada", plantillaLeida.EsPersonalizada);
Verificar("Al releerla conserva el enlace del correo", plantillaLeida.CampoCorreo == "correo");

var htmlLeida = EspacioActaRenderer.Render(plantillaLeida, valores, firmante, fecha);
Verificar("El documento renderiza igual tras el viaje a la base", htmlLeida == html);

// El ida y vuelta al diseñador tambien debe conservar todo.
var dtoVuelta = EspacioActaDisenador.ADto(plantillaLeida);
var renormalizada = EspacioActaDisenador.Normalizar(dtoVuelta, plantillaLeida.Codigo);
Verificar("La definicion vuelve del disenador sin perder nada", renormalizada.EsValida, string.Join(" | ", renormalizada.Errores));
Verificar(
    "Y renderiza identico",
    renormalizada.EsValida
    && EspacioActaRenderer.Render(renormalizada.Plantilla!, valores, firmante, fecha) == html);

// ── 5. Emisión de un acta con tres firmas ───────────────────────────────────

Seccion("5. Emision del acta");

var firmaPng = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

var estampadas = new List<EspacioActaFirmaEmitida>
{
    new() { Clave = "emisor", Rotulo = "Entrega", Nombre = firmante.Nombre, Documento = firmante.Documento, Cargo = firmante.Cargo, DataUrl = firmaPng },
    new() { Clave = "recibe", Rotulo = "Recibe", Nombre = "Yirley Yulieth Yanes", Documento = "1077463495", DataUrl = firmaPng },
    new() { Clave = "testigo", Rotulo = "Testigo", Nombre = "Jefe de Talento Humano", DataUrl = firmaPng }
};

var acta = new EspacioActaDocumental
{
    PlantillaCodigo = plantillaLeida.Codigo,
    PlantillaNombre = plantillaLeida.Nombre,
    TituloActa = plantillaLeida.TituloActa,
    NombreRecibe = "Yirley Yulieth Yanes",
    DocumentoRecibe = "1077463495",
    CorreoRecibe = "yyanes@especialistasencasa.com",
    UsuarioRecibe = null,
    ValoresJson = JsonSerializer.Serialize(valores),
    CuerpoHtml = htmlLeida,
    EmitidaPorNombre = firmante.Nombre,
    EmitidaPorCargo = firmante.Cargo,
    EmitidaPorDocumento = firmante.Documento,
    FirmaEmiteDataUrl = firmaPng,
    FirmaRecibeDataUrl = firmaPng,
    FirmasJson = JsonSerializer.Serialize(estampadas),
    FirmadaAtUtc = DateTime.UtcNow
};

db.EspacioActasDocumentales.Add(acta);
await db.SaveChangesAsync();
Verificar("El acta quedo guardada", acta.Id > 0);

db.ChangeTracker.Clear();

var actaLeida = await db.EspacioActasDocumentales.AsNoTracking().FirstAsync(x => x.Id == acta.Id);
var firmasLeidas = EspacioActaFirmas.Leer(actaLeida);

Verificar("El acta emitida devuelve sus 3 firmas", firmasLeidas.Count == 3, $"fueron {firmasLeidas.Count}");
Verificar("La tercera firma conserva su rotulo", firmasLeidas[2].Rotulo == "Testigo");
Verificar("La firma del emisor conserva el cargo", firmasLeidas[0].Cargo == firmante.Cargo);
Verificar("El cuerpo firmado se conserva tal cual", actaLeida.CuerpoHtml == htmlLeida);

// Un acta anterior al diseñador (sin FirmasJson) debe seguir mostrando dos firmas.
var actaHeredada = new EspacioActaDocumental
{
    PlantillaCodigo = EspacioActaPlantillas.CodigoAccesosTecnologicos,
    PlantillaNombre = deFabrica.Nombre,
    TituloActa = deFabrica.TituloActa,
    NombreRecibe = "Colaborador Anterior",
    DocumentoRecibe = "999999",
    ValoresJson = "{}",
    CuerpoHtml = htmlFabrica,
    EmitidaPorNombre = firmante.Nombre,
    EmitidaPorCargo = firmante.Cargo,
    EmitidaPorDocumento = firmante.Documento,
    FirmaEmiteDataUrl = firmaPng,
    FirmaRecibeDataUrl = firmaPng,
    FirmasJson = null,
    FirmadaAtUtc = DateTime.UtcNow
};

db.EspacioActasDocumentales.Add(actaHeredada);
await db.SaveChangesAsync();
db.ChangeTracker.Clear();

var heredadaLeida = await db.EspacioActasDocumentales.AsNoTracking().FirstAsync(x => x.Id == actaHeredada.Id);
var firmasHeredadas = EspacioActaFirmas.Leer(heredadaLeida);

Verificar("Un acta sin FirmasJson devuelve 2 firmas", firmasHeredadas.Count == 2);
Verificar("Y conserva el rotulo de la plantilla de fabrica", firmasHeredadas[1].Rotulo == "Recibe los accesos", firmasHeredadas[1].Rotulo);
Verificar("Y el nombre de quien recibe", firmasHeredadas[1].Nombre == "Colaborador Anterior");

// Un FirmasJson corrupto no debe tumbar la vista del acta.
heredadaLeida.FirmasJson = "{ esto no es json valido";
var firmasCorruptas = EspacioActaFirmas.Leer(heredadaLeida);
Verificar("Un FirmasJson corrupto cae al formato heredado", firmasCorruptas.Count == 2);

// ── 6. Borradores y modelos de arranque ─────────────────────────────────────

Seccion("6. Borradores y modelos de arranque");

// Lo que el usuario tenia en pantalla cuando reporto el problema: dos datos, el
// pliego vacio, sin firmas, sin titulo y sin nombre.
var aMedias = new EspacioActaDefinicionDto
{
    Nombre = "Acta de firmas",
    Descripcion = "elementos de firmas",
    Campos =
    [
        new() { Clave = "campo_1", Etiqueta = "Juan", Tipo = "Texto" },
        new() { Etiqueta = "", Tipo = "Texto" }
    ]
};

var comoPublicacion = EspacioActaDisenador.Normalizar(aMedias);
Verificar("Publicar algo a medias sigue bloqueado", !comoPublicacion.EsValida);
Verificar(
    "Y explica en espanol llano que falta",
    comoPublicacion.Errores.Any(x => x.Contains("titulo", StringComparison.OrdinalIgnoreCase)
        || x.Contains("título", StringComparison.OrdinalIgnoreCase)),
    string.Join(" | ", comoPublicacion.Errores));

var comoBorrador = EspacioActaDisenador.Normalizar(
    aMedias,
    null,
    EspacioActaDisenador.ModoDefinicion.Borrador);

Verificar("Lo mismo se puede guardar como borrador", comoBorrador.EsValida, string.Join(" | ", comoBorrador.Errores));
Verificar("El borrador conserva el dato con nombre", comoBorrador.Plantilla!.Campos.Count == 1);
Verificar("El borrador descarta el dato sin nombre", comoBorrador.Plantilla!.Campos[0].Clave == "campo_1");

var sinNombreDePlantilla = EspacioActaDisenador.Normalizar(
    new EspacioActaDefinicionDto { Descripcion = "sin nombre" },
    null,
    EspacioActaDisenador.ModoDefinicion.Borrador);
Verificar("Ni el borrador se guarda sin nombre", !sinNombreDePlantilla.EsValida);

// El campo que identifica a la persona ya no hay que elegirlo a mano.
var sinElegirNombre = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta simple",
    TituloActa = "ACTA SIMPLE",
    Campos =
    [
        new() { Clave = "quien", Etiqueta = "Nombre completo", Tipo = "Texto" }
    ],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Constancia de {{quien}}." }],
    Firmas =
    [
        new() { Rotulo = "Entrega", Origen = "Emisor" },
        new() { Rotulo = "Recibe", Origen = "EnVivo", CampoNombre = "quien" }
    ]
});
Verificar("Ya no hay que elegir a mano el dato del nombre", sinElegirNombre.EsValida, string.Join(" | ", sinElegirNombre.Errores));
Verificar("Se deduce solo", sinElegirNombre.Plantilla!.CampoNombre == "quien");

// El documento tambien: alimenta la columna por la que se busca un acta despues.
var conDocumento = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta con documento",
    TituloActa = "ACTA",
    Campos =
    [
        new() { Clave = "quien", Etiqueta = "Nombre completo", Tipo = "Texto" },
        new() { Clave = "cedula", Etiqueta = "Documento", Tipo = "Documento" }
    ],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Constancia de {{quien}} ({{cedula}})." }],
    Firmas =
    [
        new() { Rotulo = "Entrega", Origen = "Emisor" },
        new() { Rotulo = "Recibe", Origen = "EnVivo", CampoNombre = "quien" }
    ]
});
Verificar("El documento se deduce sin preguntarlo", conDocumento.Plantilla?.CampoDocumento == "cedula");

// Un correo mal enlazado no frena: simplemente no se envia copia.
var correoTorcido = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta simple",
    TituloActa = "ACTA SIMPLE",
    CampoCorreo = "quien",
    Campos = [new() { Clave = "quien", Etiqueta = "Nombre completo", Tipo = "Texto" }],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Constancia de {{quien}}." }],
    Firmas =
    [
        new() { Rotulo = "Entrega", Origen = "Emisor" },
        new() { Rotulo = "Recibe", Origen = "EnVivo", CampoNombre = "quien" }
    ]
});
Verificar("Un correo mal enlazado ya no frena el guardado", correoTorcido.EsValida, string.Join(" | ", correoTorcido.Errores));
Verificar("Y queda sin envio de copia", correoTorcido.Plantilla!.CampoCorreo is null);

// Los modelos de arranque tienen que poder publicarse tal como vienen.
Verificar("Hay modelos para empezar", EspacioActaModelos.Todos.Count >= 4);

foreach (var modelo in EspacioActaModelos.Todos)
{
    var definicionModelo = modelo.Definicion;
    definicionModelo.Nombre = modelo.Nombre;

    var revision = EspacioActaDisenador.Normalizar(
        definicionModelo,
        null,
        modelo.Clave == "blanco"
            ? EspacioActaDisenador.ModoDefinicion.Borrador
            : EspacioActaDisenador.ModoDefinicion.Publicacion);

    Verificar(
        $"El modelo '{modelo.Nombre}' queda listo tal como viene",
        revision.EsValida,
        string.Join(" | ", revision.Errores));

    if (!revision.EsValida)
    {
        continue;
    }

    var muestraModelo = EspacioActaRenderer.ValoresDeMuestra(revision.Plantilla!.Campos);
    var htmlModelo = EspacioActaRenderer.Render(revision.Plantilla!, muestraModelo, firmante, fecha);

    Verificar(
        $"El modelo '{modelo.Nombre}' se renderiza sin huecos",
        !htmlModelo.Contains("{{", StringComparison.Ordinal),
        Recorte(htmlModelo, "{{"));
}

// La negrita con un dato adentro es lo que traen los modelos: debe sobrevivir.
var conNegrita = EspacioActaDisenador.Normalizar(new EspacioActaDefinicionDto
{
    Nombre = "Acta con negrita",
    TituloActa = "ACTA",
    Campos = [new() { Clave = "quien", Etiqueta = "Nombre", Tipo = "Texto" }],
    Bloques = [new() { Tipo = "Parrafo", Texto = "Comparece **{{quien}}** ante nosotros." }],
    Firmas =
    [
        new() { Rotulo = "Entrega", Origen = "Emisor" },
        new() { Rotulo = "Recibe", Origen = "EnVivo", CampoNombre = "quien" }
    ]
}).Plantilla!;

var htmlNegrita = EspacioActaRenderer.Render(
    conNegrita,
    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) { ["quien"] = "Ana Perez" },
    firmante,
    fecha);

Verificar(
    "Un dato dentro de la negrita sale en negrita",
    htmlNegrita.Contains("<strong>Ana Perez</strong>", StringComparison.Ordinal),
    Recorte(htmlNegrita, "Ana"));

// ── 7. Limpieza ─────────────────────────────────────────────────────────────

Seccion("7. Limpieza");

await db.EspacioActasDocumentales
    .Where(x => x.Id == acta.Id || x.Id == actaHeredada.Id)
    .ExecuteDeleteAsync();

await db.EspacioActaPlantillas
    .Where(x => x.Id == entidad.Id)
    .ExecuteDeleteAsync();

var quedaronActas = await db.EspacioActasDocumentales.CountAsync(x => x.Id == acta.Id || x.Id == actaHeredada.Id);
var quedaronPlantillas = await db.EspacioActaPlantillas.CountAsync(x => x.Id == entidad.Id);

Verificar("Las actas de prueba se borraron", quedaronActas == 0);
Verificar("La plantilla de prueba se borro", quedaronPlantillas == 0);

// ── Resultado ───────────────────────────────────────────────────────────────

Console.WriteLine();
Console.WriteLine($"RESULTADO: {pruebas - fallos}/{pruebas} pruebas OK, {fallos} falla(s).");
return fallos == 0 ? 0 : 1;

static int ContarOcurrencias(string texto, string busca)
{
    var total = 0;
    var indice = texto.IndexOf(busca, StringComparison.Ordinal);
    while (indice >= 0)
    {
        total++;
        indice = texto.IndexOf(busca, indice + busca.Length, StringComparison.Ordinal);
    }

    return total;
}

static string Recorte(string texto, string cerca)
{
    var indice = texto.IndexOf(cerca, StringComparison.OrdinalIgnoreCase);
    if (indice < 0)
    {
        return "(no aparece)";
    }

    var inicio = Math.Max(0, indice - 40);
    var largo = Math.Min(120, texto.Length - inicio);
    return texto.Substring(inicio, largo).Replace("\n", " ");
}
