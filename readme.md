<div style="text-align: left;">
  <a href="#ru-версия">🇷🇺 Русский (RU)</a> |
  <a href="#eng-version">🇺🇸 English (ENG)</a>
</div>

<div id="ru-версия"></div>

**RU:**

# **QAMP** - достаточно легкий музыкальный плеер на базе .NET, созданный в первую очередь для меня

## Основные возможности

    Поддержка Hi-Res Audio (FLAC, WAW, MP3 и другие)  
    Визуализация: Встроенный и отключаемый спектр и эквалайзер  
    Статистика: Подробный анализ прослушиваний (Топ 10 треков, вес библиотеки, длительность библиотеки и другое)  
    Современный UX: Сворачивание в трей, предотварщение запуска несколько копий  

## Технологический стек

    Язык: C#  
    Платформа: .NET 11.0  
    Интерфейс: WPF  
    Звуковой движок: BASS Audio Library (Bass.Net)  
    Спектр: ScottPlot  
    БД: SQLite  
    Работа с тегами: TagLibSharp

## Быстрый старт

Скачайте последнию версию из [Releases](https://github.com/d3solat1on/QAMP/releases)  
Запустите установщик  
Готово.  

## Если ж вы хотите собрать проект самостоятельно, то

`Получите ключ на оффициальном сайте библиотеки Bass`  
`git clone https://github.com/d3solat1on/QAMP.git`  
`cd QAMP`  
`Создайте в папке проекта файл bass_settings.json`  

```json
{
  "BassEmail": "your-email@example.com",
  "BassKey": "your-bass-registration-key"
}
```

`dotnet restore`  
`dotnet build`  

### Скриншоты

<details>
<summary>Нажми, чтобы посмотреть скриншоты</summary>

![Главное меню](images/QAMP_MAIN.png)
![Подробная информация](images/QAMP_SHOW_TRACK_INFO.png)
![Редактирование](images/QAMP_EDIT_TRACK_INFO.png)
![Настройки](images/QAMP_SETTINGS.png)

</details>

## Создание кастомной темы оформления

Вы можете легко создать собственную уникальную тему без изменения кода плеера:

1. Перейдите в папку приложения `QAMP/Themes/`.
2. Скопируйте любой существующий файл (например, `DarkTheme.xaml`) и переименуйте его (например, `MyCoolTheme.xaml`).
3. Откройте этот файл в любом текстовом редакторе (Блокнот, VS Code, Notepad++).
4. Замените HEX-коды цветов (например, `#FF212121`) у ключевых ресурсов на свои:
   - `BackgroundBrush` — основной фон приложения.
   - `AccentBrush` — цвет элементов управления, акцентов и полос спектра.
   - `ForegroundBrush` — цвет основного текста.
   - `TertiaryBackgroundBrush` — подложка под визуализатор.
5. Запустите плеер, зайдите в Настройки, нажмите кнопку **«Добавить тему»** и выберите ваш созданный `.xaml` файл.

## PRIVACY

QAMP работает полностью локально. Для повышения стабильности приложение ведет текстовые логи работы (app_info.log, crash_log.txt, QAMP_SMTC.log) на вашем устройстве. Эти данные содержат исключительно техническую информацию о работе плеера и названия воспроизводимых треков для диагностики. Логи никогда и никуда не отправляются автоматически.

## История изменений

[CHANGELOG](./CHANGELOG.md)

<div id="eng-version"></div>

**ENG**

# **QAMP** is a fairly lightweight .NET-based music player created primarily for me  

## Key features

    Hi-Res Audio support (FLAC, WAW, MP3, and others)  
    Visualization: Built-in and deactivable spectrum and equalizer  
    Statistics: Detailed listening analysis (Top 10 tracks, library size, library duration, and more)  
    Modern UX: Minimize to tray, prevent multiple instances from launching  

## Tech Stack

    Language: C#  
    Platform: .NET 11.0  
    Interface: WPF  
    Audio Engine: BASS Audio Library (Bass.Net)  
    Spectrum: ScottPlot  
    Database: SQLite  
    Tag Management: TagLibSharp  

## Quick Start

Download the latest version from [Releases](https://github.com/d3solat1on/QAMP/releases)  
Run the installer  
Done.  

## If you want to build the project yourself

`Get a key from the official Bass library website`  
`git clone https://github.com/d3solat1on/QAMP.git`  
`cd QAMP`  
`Create a bass_settings.json file in the project folder`  

```json
{
"BassEmail": "your-email@example.com",
"BassKey": "your-bass-registration-key"
}
```

`dotnet restore`  
`dotnet build`

### Screenshots

<details>
<summary>Click to view screenshots</summary>  

![Main Menu](images/QAMP_MAIN.png)
![Detailed track information](images/QAMP_SHOW_TRACK_INFO.png)
![Editing a track](images/QAMP_EDIT_TRACK_INFO.png)
![Settings](images/QAMP_SETTINGS.png)
</details>

## How to Create a Custom Theme

QAMP supports fully customizable XAML themes. You can create your own look in just a minute:

1. Navigate to the `QAMP/Themes/` folder.
2. Duplicate any existing theme file (like `DarkTheme.xaml`) and rename it (e.g., `MyCustomTheme.xaml`).
3. Open the file using any text editor (Notepad, VS Code, Notepad++).
4. Change the HEX color values (e.g., `#FF212121`) for the main theme keys:
   - `BackgroundBrush` — main window background.
   - `AccentBrush` — sliders, buttons, and spectrum bar colors.
   - `ForegroundBrush` — text color.
   - `TertiaryBackgroundBrush` — visualizer background.
5. Open QAMP, go to Settings, click **"Add Theme"**, and select your custom `.xaml` file.

## PRIVACY

QAMP runs entirely locally. To improve stability, the application maintains text logs (app_info.log, crash_log.txt, QAMP_SMTC.log) on ​​your device. This data contains exclusively technical information about the player's operation and the names of tracks being played for diagnostic purposes. Logs are never sent automatically.

## Change history

[CHANGELOG](./CHANGELOG.md)
