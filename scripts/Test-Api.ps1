param([string]$BaseUrl = 'http://localhost:5080')

$ErrorActionPreference = 'Stop'
$expenseIds = [Collections.Generic.List[int]]::new()
$categoryIds = [Collections.Generic.List[int]]::new()
$script:checks = 0

function Assert-Equal($Actual, $Expected, [string]$Message) {
    if ($Actual -ne $Expected) { throw "$Message (esperado: $Expected; recebido: $Actual)" }
    $script:checks++
}

function Request([string]$Method, [string]$Path, [int]$Status, $Body = $null) {
    $parameters = @{
        Uri = "$BaseUrl$Path"
        Method = $Method
        SkipHttpErrorCheck = $true
        TimeoutSec = 30
    }
    if ($null -ne $Body) {
        $parameters.ContentType = 'application/json'
        $parameters.Body = ConvertTo-Json -InputObject $Body -Depth 10 -Compress
    }
    $response = Invoke-WebRequest @parameters
    Assert-Equal ([int]$response.StatusCode) $Status "$Method $Path"
    if ($Status -eq 204) { return $null }
    $content = $response.Content
    if ($content -is [byte[]]) { $content = [Text.Encoding]::UTF8.GetString($content) }
    $json = ConvertFrom-Json -InputObject $content
    if ($Status -ge 400) {
        Assert-Equal $json.status $Status 'Status no ProblemDetails'
    }
    return $json
}

function New-Expense([decimal]$Amount, [int]$CategoryId, [string]$Date) {
    $result = Request POST '/api/expenses' 201 @{
        description = 'Teste de integração'; amount = $Amount; date = $Date
        categoryId = $CategoryId; paymentMethod = 'Pix'
    }
    $expenseIds.Add($result.id)
    return $result
}

try {
    $openapi = Request GET '/openapi/v1.json' 200
    Assert-Equal ($null -ne $openapi.paths.'/api/expenses') $true 'Contrato OpenAPI'
    Assert-Equal ([int](Invoke-WebRequest "$BaseUrl/swagger/index.html").StatusCode) 200 'Swagger UI'
    $baseline = Request GET '/api/expenses/reports/monthly?year=2042&month=2' 200
    $suffix = [Guid]::NewGuid().ToString('N')
    $a = Request POST '/api/categories' 201 @{ name = "Teste A $suffix" }
    $categoryIds.Add($a.id)
    $b = Request POST '/api/categories' 201 @{ name = "Teste B $suffix" }
    $categoryIds.Add($b.id)
    $categories = Request GET '/api/categories' 200
    Assert-Equal @($categories | Where-Object id -EQ $a.id).Count 1 'Categoria persistida'

    $first = New-Expense 12.50 $a.id '2042-02-01'
    $second = New-Expense 7.25 $a.id '2042-02-28'
    $third = New-Expense 5 $b.id '2042-02-15'
    $outside = New-Expense 100 $a.id '2042-03-01'
    $found = Request GET "/api/expenses/$($first.id)" 200
    Assert-Equal $found.amount 12.50 'Valor persistido'
    Assert-Equal $found.categoryName $a.name 'Nome da categoria'
    $filtered = Request GET "/api/expenses?from=2042-02-01&to=2042-02-28&categoryId=$($a.id)" 200
    Assert-Equal @($filtered).Count 2 'Filtros e limites inclusivos'
    Assert-Equal $filtered[0].id $second.id 'Ordenação por data'

    $report = Request GET '/api/expenses/reports/monthly?year=2042&month=2' 200
    Assert-Equal $report.total ($baseline.total + 24.75) 'Total mensal'
    Assert-Equal ($report.categories | Where-Object categoryId -EQ $a.id).total 19.75 'Total categoria A'
    Assert-Equal ($report.categories | Where-Object categoryId -EQ $b.id).total 5 'Total categoria B'
    $null = Request DELETE "/api/categories/$($a.id)" 409

    $null = Request PUT "/api/expenses/$($first.id)" 204 @{
        description = 'Atualizada'; amount = 20; date = '2042-02-02'
        categoryId = $b.id; paymentMethod = 'Cartão'
    }
    $updated = Request GET "/api/expenses/$($first.id)" 200
    Assert-Equal $updated.amount 20 'Atualização persistida'
    Assert-Equal $updated.categoryId $b.id 'Categoria atualizada'
    $report = Request GET '/api/expenses/reports/monthly?year=2042&month=2' 200
    Assert-Equal $report.total ($baseline.total + 32.25) 'Relatório após atualização'

    foreach ($invalid in @(
        @{ description = ' '; amount = 1; date = '2042-02-01'; categoryId = $a.id; paymentMethod = 'Pix' },
        @{ description = 'Teste'; amount = 0; date = '2042-02-01'; categoryId = $a.id; paymentMethod = 'Pix' },
        @{ description = 'Teste'; amount = 1.001; date = '2042-02-01'; categoryId = $a.id; paymentMethod = 'Pix' },
        @{ description = 'Teste'; amount = 1; date = '2042-02-30'; categoryId = $a.id; paymentMethod = 'Pix' },
        @{ description = 'Teste'; amount = 1; date = '0001-01-01'; categoryId = $a.id; paymentMethod = 'Pix' },
        @{ description = 'Teste'; amount = 1; date = '2042-02-01'; categoryId = 2147483647; paymentMethod = 'Pix' },
        @{ description = 'Teste'; amount = 1; date = '2042-02-01'; categoryId = $a.id; paymentMethod = '' }
    )) { $null = Request POST '/api/expenses' 400 $invalid }
    $null = Request POST '/api/categories' 400 @{ name = ' ' }
    $null = Request GET '/api/expenses?from=2042-03-01&to=2042-02-01' 400
    $null = Request GET '/api/expenses/reports/monthly?year=2042&month=13' 400
    $null = Request GET '/api/expenses/2147483647' 404
    $null = Request DELETE '/api/categories/2147483647' 404
    $null = Request DELETE "/api/expenses/$($outside.id)" 204
    $expenseIds.Remove($outside.id) | Out-Null
    $null = Request GET "/api/expenses/$($outside.id)" 404
}
finally {
    foreach ($id in $expenseIds) { $null = Request DELETE "/api/expenses/$id" 204 }
    foreach ($id in $categoryIds) { $null = Request DELETE "/api/categories/$id" 204 }
}
Write-Output "$script:checks verificações passaram. Os dados criados pelo teste foram removidos."
