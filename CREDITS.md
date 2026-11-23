# Créditos

## Audio

Todo el audio de la primera entrega se genera con código: sintetizadores en Python (numpy) que imitan el chip de
sonido de la NES, escritos para este proyecto con ayuda de Claude (Anthropic). No usa grabaciones, samples ni
bancos de sonido de terceros, así que no hay licencias externas que cumplir.

| Qué | Archivos | Cómo se hizo |
|---|---|---|
| Canción de la bardo (dembow en 8 bits) y sus capas | `Assets/_Project/Audio/Music/BardSong_*.wav` | Composición original, `generar_dembow.py` |
| Ambiente del pueblo en silencio | `Assets/_Project/Audio/Ambience/Village_Wind_Loop.wav` | `generar_ambiente.py` |
| Pasos | `Assets/_Project/Audio/SFX/Footstep_*.wav` | `generar_ambiente.py` |
| Efectos de sonido: golpes, habilidades, daño, muertes, interrupción de la canción, hordas, victoria, derrota e interfaz | El resto de `Assets/_Project/Audio/SFX/*.wav` | `generar_sfx.py` |

La música es una composición original en ritmo de dembow: no versiona ni cita ninguna canción existente.
