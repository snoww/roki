create schema if not exists jeopardy;
set search_path = jeopardy;

create table round
(
    id   serial primary key,
    name text not null
);

insert into round (name)
values ('Jeopardy!'),
       ('Double Jeopardy!'),
       ('Final Jeopardy!');

create table category
(
    id    serial primary key,
    name  text not null,
    round int  not null references round (id)
);

create table clues
(
    id          serial primary key,
    category_id int  not null references category (id),
    text        text not null,
    value       int  not null,
    answer      text not null
);