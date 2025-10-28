name: PR Agent for NUWM

on:
  pull_request:
    types: [ opened, reopened, synchronize, edited ]

jobs:
  pr_agent_job:
    runs-on: ubuntu-latest
    name: Run pr agent on every pull request
    steps:
      - name: pr-agent-nuwm
        uses: EzGrade/Pr-Agent-NUWM@main
        env:
          GIT_APP_ID: ${{ secrets.GIT_APP_ID }}
          GIT_PRIVATE_KEY: ${{ secrets.GIT_PRIVATE_KEY }}
          GIT_INSTALLATION_ID: ${{ secrets.GIT_INSTALLATION_ID }}
          GIT_REPOSITORY: ${{ github.repository }}
          OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}
          OPENAI_MODEL: ${{ secrets.OPENAI_MODEL }}
          GOOGLE_CREDENTIALS_CONTENT: ${{ secrets.GOOGLE_CREDENTIALS_CONTENT }}
          GOOGLE_SPREADSHEET_URL: ${{ secrets.GOOGLE_SPREADSHEET_URL }}
          GOOGLE_SHEETS_NAMING: ${{ secrets.GOOGLE_SHEETS_NAMING }}
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
