param(
    [string]$ItemsPath = "data/Anniversary/items.json",
    [string]$VendorScript = ".wago-cache/vendor_price_fill.py",
    [string]$OutLua = "FrugalForge/FrugalForge_Data_VendorPrices.lua"
)

if (-not (Test-Path $ItemsPath)) {
    Write-Error "items.json not found: $ItemsPath"
    exit 1
}

if (Test-Path $VendorScript) {
    Write-Host "Updating vendor prices via $VendorScript ..."
    python $VendorScript
} else {
    Write-Warning "Vendor script not found: $VendorScript (skipping refresh)."
}

Write-Host "Generating $OutLua from $ItemsPath ..."
python -c "import json, pathlib; items=json.load(open('$ItemsPath','r',encoding='utf-8')); prices={int(i['itemId']):int(i['vendorPriceCopper']) for i in items if isinstance(i,dict) and 'itemId' in i and 'vendorPriceCopper' in i and i['vendorPriceCopper'] is not None}; lines=['FrugalForgeVendorPrices = {']+ [f'  [{k}] = {prices[k]},' for k in sorted(prices)] + ['}']; pathlib.Path('$OutLua').write_text('\\\\n'.join(lines)+'\\\\n', encoding='utf-8'); print('Wrote', len(prices), 'vendor prices')"
