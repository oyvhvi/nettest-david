create table players
(
    id uuid primary key,
    name text not null
);

create table scores
(
    score_id serial primary key,
    player_id uuid,
    play_start timestamp not null,
    time_spent interval not null,
    score int not null,
    percent_correct_answers decimal(5,2) not null,

    constraint fk_player foreign key(player_id) references players(id),
    constraint check_percent_correct_answers check(percent_correct_answers between 0.00 and 100.00)
);