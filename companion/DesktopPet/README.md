# Claude Desktop Pet for Windows

Прозрачный питомец для рабочего стола Windows: его можно перетаскивать и бросать, у него есть гравитация и отскоки от верхних поверхностей окон.

## Controls

Правый клик по Клоду открывает меню настроек в масштабе 100%, независимо от размера питомца. Там можно настроить размер, реплики, прыжки, режим «Не отвлекать», секреты, любимый угол, коллекцию, распорядок дня, свои фразы и реакцию на звук. Перетащи Клода левой кнопкой и отпусти, чтобы бросить. Клик по спящему питомцу будит его.

Settings are saved under `%LOCALAPPDATA%\ClaudeDesktopPet\settings.ini` and restored on next launch.

## Behavior and privacy

Recent keyboard/mouse activity is checked through Windows last-input time. The pet also checks keyboard key states to detect typing activity; it does not store or transmit typed text. At 10 seconds idle Claude switches to the phone pose; at 2 minutes he sleeps. He may take short tea, reading, stretching or desk-nap breaks, with morning/evening behavior controlled by the routine toggle. While working or acting out a short scene, he does not jump autonomously. In his phone/free-time pose he can hop. He turns toward a nearby cursor. Items he finds are kept in a small collection shelf near his resting place; the collection can be switched off. Window surfaces are sampled from visible top-level app windows twice per second. If a supporting window vanishes, he falls or tries a nearby ledge. This is approximate and can include windows behind the pet.

Свои фразы вводятся по одной на строку; ползунок частоты управляет тем, как часто появляются они и встроенные реплики по ситуации. Реакция на звук читает только уровень системного аудио. Питомец не определяет песню и не распознаёт речь, аудио не сохраняется. Если устройство вывода отключится, меню покажет статус ошибки; нажми кнопку звука для повторного подключения.

## Build

```powershell
dotnet publish .\DesktopPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
