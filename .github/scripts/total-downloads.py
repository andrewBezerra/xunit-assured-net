#!/usr/bin/env python3
# Soma os downloads de todos os pacotes do dono no NuGet e escreve o JSON de um badge do shields.io
# (https://shields.io/badges/endpoint-badge).
#
# A API de busca do NuGet responde por réplicas regionais, e elas não andam juntas: em 2026-10-16
# o XUnitAssured.Core tinha 401 downloads numa, 797 noutra e 6.345 noutra (o número do site).
# Contador de download só cresce, então o maior valor de cada pacote entre as réplicas é o mais
# atualizado. E o total nunca desce do que a rodada anterior gravou.
#
# Uso: total-downloads.py <dono> <diretório>. Escreve ali downloads.json (o badge) e
# downloads-total.txt (o número exato, que a próxima rodada lê).
import json
import os
import sys
import urllib.request

REPLICAS = ["usnc", "ussc", "ea", "sea"]


def downloads_por_replica(replica, dono):
    url = (f"https://azuresearch-{replica}.nuget.org/query?q=owner:{dono}"
           "&take=1000&prerelease=true&semVerLevel=2.0.0")
    with urllib.request.urlopen(url, timeout=30) as resposta:
        dados = json.load(resposta)
    return {p["id"].lower(): (p["id"], p["totalDownloads"]) for p in dados["data"]}


def abreviado(n):
    # Como o shields.io escreve os downloads nos badges do NuGet: 27.7k, 1.2M.
    for limite, sufixo in ((1_000_000, "M"), (1_000, "k")):
        if n >= limite:
            return f"{n / limite:.1f}".removesuffix(".0") + sufixo
    return str(n)


def main(dono, diretorio):
    maior = {}
    for replica in REPLICAS:
        try:
            pacotes = downloads_por_replica(replica, dono)
        except Exception as erro:  # uma réplica fora do ar não impede a soma
            print(f"::warning::NuGet search replica {replica} failed: {erro}")
            continue
        for chave, (nome, total) in pacotes.items():
            if chave not in maior or total > maior[chave][1]:
                maior[chave] = (nome, total)
    if not maior:
        sys.exit("No NuGet search replica answered; keeping the previous badge.")

    total = sum(t for _, t in maior.values())
    arquivo_total = os.path.join(diretorio, "downloads-total.txt")
    try:
        with open(arquivo_total, encoding="utf-8") as anterior:
            total = max(total, int(anterior.read().strip() or 0))
    except FileNotFoundError:
        pass

    for nome, t in sorted(maior.values(), key=lambda p: -p[1]):
        print(f"{nome}: {t}")
    print(f"Total: {total}")

    badge = {"schemaVersion": 1, "label": "downloads (all packages)",
             "message": abreviado(total), "color": "blue"}
    with open(os.path.join(diretorio, "downloads.json"), "w", encoding="utf-8") as saida:
        json.dump(badge, saida, indent=2)
        saida.write("\n")
    with open(arquivo_total, "w", encoding="utf-8") as saida:
        saida.write(f"{total}\n")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
