<p align="center"><img src="docs/logo.svg" width="96" alt="" /></p>

# ratesim

ratesim is an offline command-line tool for people who size rate limits for an API or a gateway. It replays a request log through candidate rate-limit policies and says how many requests each policy would have allowed and denied (`ratesim run`), why each single request got its answer (`ratesim explain`) and the smallest limit that keeps denials under a target (`ratesim suggest`). It works offline and never changes its input.

[![CI](https://github.com/Gulliby-development/ratesim/actions/workflows/ci.yml/badge.svg)](https://github.com/Gulliby-development/ratesim/actions/workflows/ci.yml) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Install

Build the tool with:

```
dotnet build -c Release
```

Run it with:

```
dotnet run --project src/Ratesim -- ARGS
```

## Usage

```
usage: ratesim run --policy SPEC [--policy SPEC]... [--max-denied PERCENT] [--scope key|all] [--format text|csv] LOG
       ratesim explain --policy SPEC [--only allowed|denied] [--scope key|all] LOG
       ratesim suggest --kind fixed|sliding --window DURATION [--max-denied PERCENT] [--scope key|all] LOG
```

LOG is a path or `-` (standard input). After a lone `--` the next argument is the LOG even when it starts with `-` (`ratesim run --policy fixed:3/10s -- -api.log`). The examples below use this `api.log`:

```
# api gateway sample
2024-05-01T10:00:00.000Z alice
2024-05-01T10:00:01.000Z alice
2024-05-01T10:00:01.500Z bob
2024-05-01T10:00:02.000Z alice 2
2024-05-01T10:00:09.900Z alice
2024-05-01T10:00:10.000Z alice
2024-05-01T10:00:10.200Z alice
2024-05-01T10:00:10.300Z bob
2024-05-01T10:00:11.000Z alice
2024-05-01T10:00:12.000Z bob 3
```

`ratesim run --policy fixed:3/10s --policy sliding:3/10s --policy bucket:1/5s,2 api.log` prints

```
10 requests from 2 keys
policy         allowed  denied  denied%
fixed:3/10s          8       2    20.0%
sliding:3/10s        7       3    30.0%
bucket:1/5s,2        6       4    40.0%
```

With `--format csv`, `run` prints one comma-separated row per policy instead of the table, under the header `policy,allowed,denied,denied_percent`. The request and key counts are left out, the percentage has no `%` sign, and a policy that holds a comma is written in double quotes. `--format text` is the default, only `run` accepts `--format`, and the exit code still follows `--max-denied`. `ratesim run --policy fixed:3/10s --policy bucket:1/5s,2 --format csv api.log` prints

```
policy,allowed,denied,denied_percent
fixed:3/10s,8,2,20.0
"bucket:1/5s,2",6,4,40.0
```

`ratesim explain --policy bucket:1/5s,2 api.log` prints

```
2 2024-05-01T10:00:00.000Z alice allowed (tokens left: 1)
3 2024-05-01T10:00:01.000Z alice allowed (tokens left: 0)
4 2024-05-01T10:00:01.500Z bob allowed (tokens left: 1)
5 2024-05-01T10:00:02.000Z alice denied (tokens left: 0)
6 2024-05-01T10:00:09.900Z alice allowed (tokens left: 0)
7 2024-05-01T10:00:10.000Z alice allowed (tokens left: 0)
8 2024-05-01T10:00:10.200Z alice denied (tokens left: 0)
9 2024-05-01T10:00:10.300Z bob allowed (tokens left: 1)
10 2024-05-01T10:00:11.000Z alice denied (tokens left: 0)
11 2024-05-01T10:00:12.000Z bob denied (cost 3 exceeds burst 2)
6 allowed, 4 denied
```

`ratesim explain --only denied --policy bucket:1/5s,2 api.log` prints only the denied requests; the last line still counts every request:

```
5 2024-05-01T10:00:02.000Z alice denied (tokens left: 0)
8 2024-05-01T10:00:10.200Z alice denied (tokens left: 0)
10 2024-05-01T10:00:11.000Z alice denied (tokens left: 0)
11 2024-05-01T10:00:12.000Z bob denied (cost 3 exceeds burst 2)
6 allowed, 4 denied
```

`--only allowed` prints only the allowed requests. Only `explain` takes `--only`.

`ratesim suggest --kind fixed --window 10s api.log` prints

```
suggested: fixed:5/10s
denied 0 of 10 requests (0.0%)
```

`ratesim suggest --kind fixed --window 10s --max-denied 20 api.log` prints

```
suggested: fixed:3/10s
denied 2 of 10 requests (20.0%)
```

## Log format

The log is UTF-8 text (one leading BOM is skipped). Lines are split at LF; one CR before it is removed. Blank lines and lines whose first non-blank character is `#` are skipped, but every line counts for line numbers.

A request line is `TIME KEY` or `TIME KEY COST`, separated by spaces or tabs. TIME is UTC `YYYY-MM-DDTHH:MM:SSZ` with an optional fraction of 1 to 3 digits before the `Z`, year 1970 or later. KEY is any text without blanks. COST is an integer from 1 to 999999 and defaults to 1. Times must not go backwards; equal times are fine.

## Policies

- `fixed:LIMIT/WINDOW` cuts time into windows of WINDOW and allows a request when the cost already allowed in the key's current window plus its cost is at most LIMIT.
- `sliding:LIMIT/WINDOW` allows a request when the cost of the key's allowed requests in the last WINDOW (a request exactly WINDOW ago no longer counts) plus its cost is at most LIMIT.
- `bucket:RATE/WINDOW,BURST` is a token bucket per key that starts full with BURST tokens, gains RATE tokens every WINDOW and allows a request when it holds at least its cost in tokens.

A duration is an integer from 1 to 999999 followed by `ms`, `s`, `m` or `h`, at most 24 hours. `--scope all` makes every request share one state.

## Exit codes

- 0: success.
- 1: `--max-denied` exceeded (`run` only).
- 2: usage, input or log error.

## Development

```
dotnet build
dotnet format --verify-no-changes
dotnet test
```

## License

MIT, see [LICENSE](LICENSE).

---

Built with BAKED, an open-source coding agent.
