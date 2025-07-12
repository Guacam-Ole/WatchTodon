
## Account hinzufügen
Um einen Watchdog hinzuzufügen einfach
`ADD [user] [interval in hours]`
eingebn.

Beispiel:

`Add @guacamole@chaos.social 10`

`Add guacamole@chaos.social 10`

Erstellt einen Watchdog für "@guacamole@chaos.social" der dich informiert wenn der Account seit 10 Stunden nichts veröffentlicht hat. Der Unterschied ist, dass ohne das "@" der betreffende Account nicht genervt wird mit Mentions. Wenn das Ziel ein Bot ist, kein Problem, ein Mensch interessiert sich ggf. nicht für Deine Überwachungsaufgaben.



## Account entfernen
`REMOVE [user]`

Beispiel:

`REMOVE @guacamole@chaos.social`

`REMOVE guacamole@chaos.social`

Entfernt den Watchdog für diesen Account

## Watchdogs zeigen
`INFO`

Zeigt Deine gespeicherten Watchdogs