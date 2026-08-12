# =============================================================================
# Autoprueba del puente Nexa -> Supabase (lado intranet).
#
# Verifica que la normalizacion y el HMAC de C# producen exactamente los mismos
# valores que la Edge Function, usando los vectores compartidos de
# supabase/functions/sync-pacientes-heridas/test-vectors.json.
#
# Ejecutar desde la raiz del repositorio:
#     dotnet build Nexa.csproj
#     pwsh tools/bridge-selftest.ps1
#
# Carga por reflexion el Nexa.dll ya compilado, asi que prueba el codigo real de
# Helpers/BridgeIdentityNormalizer.cs. No necesita base de datos ni credenciales.
# =============================================================================

$ErrorActionPreference = 'Stop'

if (-not (Test-Path 'Nexa.csproj')) {
    Write-Error 'Ejecuta el script desde la raiz del repositorio (donde esta Nexa.csproj).'
}

$ensamblado = Join-Path (Get-Location) 'bin/Debug/net10.0/Nexa.dll'
if (-not (Test-Path $ensamblado)) {
    Write-Error "No se encontro $ensamblado. Ejecuta primero: dotnet build Nexa.csproj"
}

# Nexa.dll arrastra dependencias que PowerShell no busca solo: unas estan en la
# carpeta de salida y otras (ASP.NET Core) en el framework compartido de .NET.
$script:rutasSondeo = @(Split-Path $ensamblado -Parent)
$frameworkAspNet = Join-Path (Split-Path (Get-Command dotnet).Source -Parent) 'shared/Microsoft.AspNetCore.App'
if (Test-Path $frameworkAspNet) {
    $script:rutasSondeo += (Get-ChildItem $frameworkAspNet -Directory |
        Sort-Object { [version]$_.Name } -Descending |
        Select-Object -First 1).FullName
}

[System.Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving({
        param($contexto, $nombre)
        foreach ($carpeta in $script:rutasSondeo) {
            $ruta = Join-Path $carpeta "$($nombre.Name).dll"
            if (Test-Path $ruta) { return $contexto.LoadFromAssemblyPath($ruta) }
        }
        return $null
    })

Add-Type -Path $ensamblado
$normalizador = [Nexa.Helpers.BridgeIdentityNormalizer]

$vectores = Get-Content 'supabase/functions/sync-pacientes-heridas/test-vectors.json' -Raw | ConvertFrom-Json
$secreto = $vectores.secret

$script:pruebas = 0
$script:fallos = 0

function Check([string]$caso, [string]$esperado, [string]$obtenido) {
    $script:pruebas++
    if ($esperado -ceq $obtenido) {
        Write-Host "  ok    $caso"
    }
    else {
        $script:fallos++
        Write-Host "  FALLO $caso" -ForegroundColor Red
        Write-Host "        esperado: $esperado"
        Write-Host "        obtenido: $obtenido"
    }
}

function CheckDistinto([string]$caso, [string]$a, [string]$b) {
    $script:pruebas++
    if ($a -ceq $b) {
        $script:fallos++
        Write-Host "  FALLO $caso (los dos valores son iguales)" -ForegroundColor Red
    }
    else {
        Write-Host "  ok    $caso"
    }
}

Write-Host 'Normalizacion y HMAC de documento'
foreach ($vector in $vectores.documents) {
    $normalizado = $normalizador::NormalizeDocument($vector.input)
    Check "normalizar `"$($vector.input)`"" $vector.normalized $normalizado
    Check "hmac de `"$($vector.input)`"" $vector.hmac $normalizador::ComputeHmacHex($secreto, $normalizado)
}

Write-Host "`nNormalizacion y HMAC de nombre"
foreach ($vector in $vectores.names) {
    $normalizado = $normalizador::NormalizeName($vector.input)
    Check "normalizar `"$($vector.input)`"" $vector.normalized $normalizado
    Check "hmac de `"$($vector.input)`"" $vector.hmac $normalizador::ComputeHmacHex($secreto, $normalizado)
}

Write-Host "`nCasos limite"
Check 'documento vacio' '' $normalizador::NormalizeDocument('   ')
Check 'documento solo separadores' '' $normalizador::NormalizeDocument('..--  //')
Check 'documento nulo' '' $normalizador::NormalizeDocument($null)
Check 'nombre vacio' '' $normalizador::NormalizeName('   ')
Check 'nombre solo separadores' '' $normalizador::NormalizeName('-- .. --')
Check 'nombre nulo' '' $normalizador::NormalizeName($null)

Write-Host "`nIdempotencia (mismo paciente escrito distinto)"
Check 'documento con y sin puntos' `
    $normalizador::ComputeHmacHex($secreto, $normalizador::NormalizeDocument('1.234.567-8')) `
    $normalizador::ComputeHmacHex($secreto, $normalizador::NormalizeDocument('  12345678 '))
Check 'nombre con espacios y tildes' `
    $normalizador::ComputeHmacHex($secreto, $normalizador::NormalizeName('JOSE PEREZ')) `
    $normalizador::ComputeHmacHex($secreto, $normalizador::NormalizeName('  josé   pérez '))

Write-Host "`nEl HMAC depende del secreto (no es SHA-256 simple)"
CheckDistinto 'cambiar el secreto cambia el digest' `
    $normalizador::ComputeHmacHex('secreto-a', '12345678') `
    $normalizador::ComputeHmacHex('secreto-b', '12345678')
CheckDistinto 'el HMAC no coincide con SHA-256 simple' `
    $normalizador::ComputeHmacHex($secreto, '12345678') `
    'ef797c8118f02dfb649607dd5d3f8c7623048c9c063d532cc95c5ed7a898a64f'

Write-Host "`nFirma de peticion"
$firma = $normalizador::ComputeRequestSignature('api-secret', '1760000000', 'req-1', '{"a":1}')
Check 'firma reproducible' $firma $normalizador::ComputeRequestSignature('api-secret', '1760000000', 'req-1', '{"a":1}')
CheckDistinto 'la firma cambia al alterar el cuerpo' $firma `
    $normalizador::ComputeRequestSignature('api-secret', '1760000000', 'req-1', '{"a":2}')
CheckDistinto 'la firma cambia al alterar el timestamp' $firma `
    $normalizador::ComputeRequestSignature('api-secret', '1760000001', 'req-1', '{"a":1}')
CheckDistinto 'la firma cambia al alterar el requestId' $firma `
    $normalizador::ComputeRequestSignature('api-secret', '1760000000', 'req-2', '{"a":1}')
CheckDistinto 'la firma cambia con otro secreto' $firma `
    $normalizador::ComputeRequestSignature('otro-secreto', '1760000000', 'req-1', '{"a":1}')

Write-Host "`n$($script:pruebas - $script:fallos)/$($script:pruebas) comprobaciones correctas."
exit ($(if ($script:fallos -eq 0) { 0 } else { 1 }))
