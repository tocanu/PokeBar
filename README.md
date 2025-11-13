# PokéBar

Aplicativo WPF (.NET 8) tipo idle que vive sobre a barra de tarefas do Windows, animando um Pokémon (SpriteCollab) com interações e batalhas casuais.

## Requisitos
- .NET 8 SDK
- Windows 10/11

## Executar (dev)
```
cd PokeBar
# Opcional: definir raiz dos sprites
# $env:POKEBAR_SPRITE_ROOT = 'C:\\Users\\Arthur\\Documents\\Projetos\\SpriteCollab\\sprite'

dotnet run
```

## Publicar (single-file)
```
dotnet publish PokeBar.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o .\\publish
```

## Caminho dos sprites
Por padrão o app usa `C:\\Users\\Arthur\\Documents\\Projetos\\SpriteCollab\\sprite`. Você pode alterar via variável de ambiente `POKEBAR_SPRITE_ROOT`.

## Save
O estado (JSON) fica em `%APPDATA%\\.pokebar\\save.json`.

## Atalhos de bandeja
- PokéCenter (Curar)
- PokéMart (Comprar)
- PC (Organizar)
- Inverter transição multimonitor
- Ajuste Fino de Altura (+/- 1 px e reset)
- Mostrar/Ocultar
- Salvar Agora
- Sair

## Estrutura
- MVVM: `ViewModels/`, `Views/`
- Serviços: sprites, taskbar, tray, batalhas, salvamento
- Janela transparente 96x48 sempre no topo, integra com a taskbar e multi‑monitor

## Créditos de sprites
Compatível com SpriteCollab (PMD). Coloque os assets na raiz configurada, ex.: `...\\0287\\Walk-Anim.png`.

