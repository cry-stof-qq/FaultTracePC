using Xunit;

// La langue est un état GLOBAL (Lang.Current). Plusieurs tests la basculent le
// temps d'une vérification, et plusieurs autres affirment du texte français.
// Exécutés en parallèle, les seconds lisent la langue changée par les premiers
// et échouent une fois sur deux, sans rapport avec ce qu'ils vérifient.
//
// La collection « Langue » ne protège que les tests qui la rejoignent : elle ne
// dit rien de ceux des autres collections, qui tournent en même temps. La seule
// barrière fiable est donc à l'échelle de l'assemblage — et elle ne coûte rien,
// la suite entière tourne en moins de deux secondes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
