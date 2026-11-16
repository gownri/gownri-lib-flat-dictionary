# FlatDictionary

> **Work in progress.** This repository is under active development. The API is not yet stable, and this README is a placeholder that will be expanded as the project matures.

`FlatDictionary<TKey, TValue>` is a concurrent, index-oriented dictionary for .NET. In addition to the usual key-based lookup, it can return the internal slot index of an entry (`GetIndex`), and it exposes its keys and values as contiguous spans (`Keys` / `Values`), so entries can be accessed efficiently by index.

Main operations: `GetOrAdd`, `GetIndex`, `TryGetValue`, `TryRemove`, and `Clear`.

Target framework: .NET Standard 2.1.
