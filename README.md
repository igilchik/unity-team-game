# Unity Team Game

Tímový projekt v Unity (UKF).

## Pred prvým použitím
1. Nainštaluj [Git](https://git-scm.com/).
2. Všetci používajú **rovnakú verziu Unity** (je zapísaná v `ProjectSettings/ProjectVersion.txt`).
3. Naklonuj repozitár: `git clone https://github.com/igilchik/unity-team-game.git`
4. Otvor priečinok v Unity Hub cez **Add project from disk**.

## Ako pracovať
- Nepracuj priamo v `main`. Pre každú úlohu si sprav vetvu: `git checkout -b meno/uloha`
- Pred začatím práce: `git pull`
- Po práci: `git add .` → `git commit -m "čo som urobil"` → `git push`
- Na GitHube otvor **Pull Request** do `main`.
- **V jednej scéne naraz pracuje len jeden človek**, scény sa zle zlučujú. Dohodnite sa v chate.
- Svoje veci dávaj do vlastných prefabov, nie priamo do spoločnej scény.
- Nenahrávaj súbory väčšie ako 50 MB (GitHub ich odmietne od 100 MB).

## Nastavenie Unity (raz pri vytvorení projektu)
- Edit → Project Settings → Editor → Version Control: **Visible Meta Files**
- Asset Serialization: **Force Text**
