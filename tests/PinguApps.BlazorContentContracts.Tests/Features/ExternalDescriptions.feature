Feature: Consumer owned external descriptions

    Scenario Outline: Validate descriptions against referenced symbols
        Given an external description '<description>'
        When external description analysis runs
        Then external description errors number <count>

        Examples:
            | description | count |
            | transparent | 0 |
            | trusted | 0 |
            | missing trust | 1 |
            | invalid target | 1 |
            | unknown slot | 1 |
            | duplicate slot | 1 |
            | duplicate | 2 |
            | null types | 1 |
            | invalid html | 1 |
            | forwarding alone | 1 |
            | generic | 0 |
            | closed generic | 0 |
            | overlapping generic | 2 |
