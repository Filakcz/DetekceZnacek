# DetekceZnacek

[![License](https://img.shields.io/github/license/Filakcz/DetekceZnacek?style=for-the-badge)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-blue?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Made%20with-Avalonia-00a2ed?style=for-the-badge)](https://avaloniaui.net/)


**Detekce dopravních značek** pomocí:
- Template matching s metodou RMSE/TM_CCOEFF_NORMED
- Histogram orientovaných gradientů s k-NN

![Screenshot](Screenshot/screenshot.png)


## Tutoriál

### 1. Stáhnutí aplikace
Z [GitHub Releases](https://github.com/Filakcz/DetekceZnacek/releases) se dá stáhnout nejnovější zkompilovaná aplikace (Windows or Linux)

Na Linuxu se musí použít `chmod +x DetekceZnacek` a potom `./DetekceZnacek`. 

### 2. Spuštění aplikace
Ve stejné složce jako aplikace musí být složka `Vzory`, které obsahuje vzory dopravních značek. Bez složky se aplikace nespustí.

### Jeden obrázek
Stačí nahrát obrázek (`.png`, `.jpg`, `.jpeg`, `.bmp`) v tabu `Detekce`. Sliderem jde vidět barvy v masce a finální obrysy. Klasifikace tohoto obrázku je v dalším tabu `Klasifikace`.

### Složka s řešením
V tabu `Test složky` se jde nahrát složka, která musí obsahovat soubor `Reseni.txt` a obrázky, které mají stejný název jako v řešení. Zpracování složky trvá několik sekund. Syntax řešení je následovně:

```
nazev_obrazku=znacka1;znacka2;znacka3

5=hlavni;prechod_chodci;zakaz_odboceni_vlevo
```
## License
This project is licensed under the [Apache License 2.0](LICENSE).
