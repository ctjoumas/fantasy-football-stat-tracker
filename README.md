# fantasy-football-stat-tracker
[![Build and deploy ASP.Net Core app to Azure Web App - fantasyfootballstattracker](https://github.com/ctjoumas/fantasy-football-stat-tracker/actions/workflows/master_fantasyfootballstattracker.yml/badge.svg)](https://github.com/ctjoumas/fantasy-football-stat-tracker/actions/workflows/master_fantasyfootballstattracker.yml)

Displays live game stats for players, displaying them in a head to head matchup. All player information is pulled back from the Yahoo Sports API (https://developer.yahoo.com/fantasysports/guide/). However, since this API doesn't provide live stats, the fantasy points scored by a player are retrieved by scraping the HTML at ESPN's gametracker and play-by-play pages.
