# Dateiname:
Format ist irgendwas + ".board"

Beispiel:
```
Example.board
```



# Kommentare
Leerzeilen und Zeilen die beginnen mit "# " werden ignoriert.

Beispiel:
```
# This is a comment
# This, too
```



# Adressierung von Zellen:
Der Index einer Zelle ist wahlweise
a) eine Zahl, z.B. "5". Das ist die wievielte Zelle es ist, gelesen von links nach rechts, dann oben nach unten, beginnen bei 0.
b) zwei Zahlen mit Komma dazwischen, z.B. "2,4". Das sind die Koordinaten der Zelle, erst Y, dann X, beides beginnt bei 0.
Die Zelle ganz links oben kann man also mit "0" oder mit "0,0" adressieren.
Die letzte Zelle in einem Brett mit 2 Zeilen und 3 Spalten ist "1,2" oder "5".



# Befehle
Immer exakt ein Befehl pro Zeile.
Jedes(!) Zeichen wird beachtet, auch Whitespace, also präzise tippen.


## Brett initialisieren
"board " und die offenen (0) und geschlossenen (1) Zellen.
Nach jeder Zeile/Reihe ein Komma. Am Ende kein Komma.

Beispiel:
```
board 01011,00001,11010,00000

erzeugt

[]XX[]XXXX
[][][][]XX
XXXX[]XX[]
[][][][][]
```


## Zellen verbinden/trennen
"merge A B" mit A und B als Index der Zellen.
Die Zellen sind dann verbunden.
"split A B" ist das gleiche aber mit Trennen statt Verbinden.


## Brett finalisieren
Wenn das Brett bereit ist muss "inst" folgen um es zu instanziieren.
Das kommt immer NACH "board", "merge", "split" und VOR "cell" oder "col"


## Zellen färben
"col" + Farbe + Indizes der Zellen, getrennt durch Leerzeichen

Beispiel:
```
col blue 4 3 2,1

färbt drei Zellen: Zelle 4, Zelle 3 und Zelle mit Koordinaten Y=2,X=1 (0-basiert)
```

Es sind diese Farben vordefiniert:
1. blue
2. green
3. brown
4. red
Sie sollten wegen Farbenblindheit in dieser Reihenfolge verwendet werden (d.h. wenn man nur zwei Farben braucht, dann nur blue und green).

Neue Farben kann man definieren mittels "defcol" + Farbcode + Name
Farbcode ist HTML Farbe z.B. "ff0000" ist rot.
Beispiel:
```
defcol 850085 deepPurple
```


## Zelle benennen
"cell" + Index + Name
Name = Inschrift, was in der Zelle steht.
Der Name ändert nichts am Index! Dient nur der Anzeige.
Name darf Latex oder Mathmode sein.
Wenn kein einziger Name vergeben wird dann steht in allen Zellen deren Index.

Beispiel:
```
cell 0 A
Zelle ganz oben links heißt nun "A".

cell 0 1
In Zelle 0 steht nun "1" - die Zelle hat aber weiterhin Index 0.

cell 1,0 $x^2$
Erste Zelle in zweiter Zeile ist nun x-Quadrat.
Wird nur in Latex angezeigt, nicht im PNG.

cell 0,1 \textbf{m}
In der zweiten Zelle der ersten Zeile steht fettgedruckt "m".
Wird nur in Latex angezeigt, nicht im PNG.
```


## Zeichnen
Am Ende von allen obigem, erzeugt PNG und TIKZ.
```
draw png
```

## Andere Befehle, nicht relevant:

- fade
- connectible-white



# Beispiel

board 01011,00001,11010,00000
split 15 16
merge 0 5
merge 0 6
merge 0 7
merge 0 8
inst

cell 2 A
cell 14 C
cell 15 B
cell 17 P
cell 19 r
cell 16 $s_1$
cell 12 $s_2$
cell 18 $s_3$
col brown 0 5 6 7 8
col green 12 16 18
col red 19
col blue 17

draw png
